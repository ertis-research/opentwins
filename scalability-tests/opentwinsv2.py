# platformv2.py

import time, json, random
from datetime import datetime
from uuid import uuid4
import paho.mqtt.client as mqtt
from concurrent.futures import ThreadPoolExecutor
import psycopg2
from dateutil import parser
from dotenv import load_dotenv
import os

load_dotenv()

MQTT_CONF = {
    "host": os.getenv("MQTT_HOST"),
    "port": int(os.getenv("MQTT_PORT", 1883)),
    "topic": os.getenv("MQTT_TOPIC")
}

PG_CONF = {
    "host": os.getenv("PG_HOST"),
    "dbname": os.getenv("PG_DBNAME"),
    "user": os.getenv("PG_USER"),
    "password": os.getenv("PG_PASSWORD")
}

_sent_map = {}

def prepare_test(num_devices, update_interval, test_duration):
    print(f"[OpenTwinsV2] Preparing test with {num_devices} devices, interval {update_interval}s, duration {test_duration}s.")
    _sent_map.clear()

def generate_payload(uuid, t_sent):
    return {
        "uuid": uuid,
        "t_sent": t_sent,
        "temperature": round(random.uniform(20.0, 30.0), 2),
        "humidity": round(random.uniform(40.0, 60.0), 2),
    }

def simulate_device(thing_id, update_interval, test_duration):
    client = mqtt.Client()
    client.username_pw_set(MQTT_CONF["username"], MQTT_CONF["password"])
    client.connect(MQTT_CONF["host"], MQTT_CONF["port"], 60)
    client.loop_start()

    topic = MQTT_CONF["topic"].format(thing_id=thing_id)
    start_time = time.time()

    while time.time() - start_time < test_duration:
        uid = str(uuid4())
        t_sent = datetime.utcnow().isoformat() + "Z"
        _sent_map[uid] = t_sent

        payload = generate_payload(uid, t_sent)
        client.publish(topic, json.dumps(payload))
        time.sleep(update_interval)

    client.loop_stop()
    client.disconnect()

def get_written_times_pg(sent_map):
    conn = psycopg2.connect(**PG_CONF)
    cur = conn.cursor()
    results = {}

    for uid in sent_map.keys():
        cur.execute("SELECT time FROM telemetry WHERE uuid = %s ORDER BY time DESC LIMIT 1", (uid,))
        row = cur.fetchone()
        if row:
            results[uid] = row[0].isoformat()

    cur.close()
    conn.close()
    return results

def run_test(num_devices, update_interval, test_duration):
    print("[OpenTwinsV2] Running test...")
    with ThreadPoolExecutor(max_workers=num_devices) as executor:
        for i in range(num_devices):
            executor.submit(simulate_device, f"device_{i}", update_interval, test_duration)

    print("[OpenTwinsV2] Waiting for data to appear in TimescaleDB...")
    time.sleep(5)
    written_map = get_written_times_pg(_sent_map)

    latencies = []
    for uid, t_sent in _sent_map.items():
        if uid in written_map:
            t1 = parser.isoparse(t_sent)
            t2 = parser.isoparse(written_map[uid])
            latencies.append((t2 - t1).total_seconds())

    return latencies
