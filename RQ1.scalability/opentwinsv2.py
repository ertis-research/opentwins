import time, json, random
from datetime import datetime, timezone
from uuid import uuid4
import paho.mqtt.client as mqtt
from concurrent.futures import ThreadPoolExecutor
import pandas as pd
import psycopg2
from psycopg2.extras import RealDictCursor
from dateutil import parser
from dotenv import load_dotenv
import os
import httpx
import asyncio
import requests
import threading

load_dotenv()

MQTT_CONF = {
    "host": os.getenv("MQTT_V2_HOST"),
    "port": int(os.getenv("MQTT_V2_PORT", 1883)),
    "topic": os.getenv("MQTT_V2_TOPIC")
}

PG_CONF = {
    "host": os.getenv("PG_HOST"),
    "port": os.getenv("PG_PORT"),
    "dbname": os.getenv("PG_DBNAME"),
    "user": os.getenv("PG_USER"),
    "password": os.getenv("PG_PASSWORD")
}

OPENTWINSV2_CONF = {
    "things": os.getenv("OTV2_THINGS_URL"),
    "twins": os.getenv("OTV2_TWINS_URL")
}

headers = {
    "Content-Type": "application/json"
}

def get_default_TD(num):
    return {
        "@context": "https://www.w3.org/2022/wot/td/v1.1",
        "title": "Scalability test",
        "id": f"test:device{num}",
        "properties": {
            "humidity": {
                "title": "Humidity",
                "type": "number"
            },
            "temperature": {
                "title": "Temperature",
                "type": "number"
            }
        },
        "otv2:subscribedEvents": [
            {
                "otv2:event": f"mqtt:changes_device{num}",
                "otv2:autoEmitState": True
            }
        ]
    }

_sent_map_v2 = {}
_sent_map_v2_lock = threading.Lock()

mid_to_uid_v2 = {}
mid_to_uid_v2_lock = threading.Lock()


async def prepare_test(num_devices, update_interval, test_duration):
    print(f"[OpenTwinsV2] Preparing test with {num_devices} devices, interval {update_interval}s, duration {test_duration}s.")
    
    semaphore = asyncio.Semaphore(100)  # limita a 100 peticiones concurrentes

    async with httpx.AsyncClient(timeout=15) as client:
        async def post_device(i):
            device_name = f"test:device{i}"
            url = OPENTWINSV2_CONF.get("things") + "/things"
            td_payload = get_default_TD(str(i))

            async with semaphore:
                try:
                    response = await client.post(url, headers=headers, json=td_payload)
                    if not (200 <= response.status_code < 300):
                        print(f"[ERROR] Failed to initialize {device_name}. Status code: {response.status_code}, Response: {response.text}")
                except httpx.TimeoutException:
                    print(f"[ERROR] Timeout while initializing {device_name}")
                except httpx.RequestError as e:
                    print(f"[ERROR] Error while initializing {device_name}: {repr(e)}")

        tasks = [post_device(i) for i in range(num_devices)]
        await asyncio.gather(*tasks)

    _sent_map_v2.clear()

def generate_payload(uid):
    return {
        "uid": uid,
        "temperature": round(random.uniform(20.0, 30.0), 2),
        "humidity": round(random.uniform(40.0, 60.0), 2),
    }

def on_publish(client, userdata, mid):
    full_mid = userdata["name"] + "_" + str(mid)
    
    uid = None
    t_sent = None
    
    with mid_to_uid_v2_lock:
        value = mid_to_uid_v2.pop(full_mid, None)
        if value:
            uid, t_sent = value
        
    if uid and t_sent:
        with _sent_map_v2_lock:
            _sent_map_v2[uid] = t_sent 
    #else:
        #print(f"[WARNING] Published mid={full_mid}, but uid not found")
        

def simulate_device(name, update_interval, test_duration, stop_event):
    time.sleep(random.uniform(0.01, 0.2)) # para evitar que todos intenten conectarse a la vez
    client = mqtt.Client(userdata={"name": name}, client_id=f"device_{name}")
    client.on_publish = on_publish
    client.max_inflight_messages_set(10000)
    client.max_queued_messages_set(1000000)
    client.reconnect_delay_set(min_delay=1, max_delay=10)
    client.connect(MQTT_CONF["host"], MQTT_CONF["port"])
    client.loop_start()

    start_time = time.time()
    topic = MQTT_CONF["topic"] + name
    
    payload = generate_payload(str(uuid4()))
    client.publish(topic, json.dumps(payload)) ## cold-start
    time.sleep(1)

    while time.time() - start_time < test_duration and not stop_event.is_set():
        uid = str(uuid4())
        t_sent = datetime.now(timezone.utc).isoformat()

        payload = generate_payload(uid)
        msg = client.publish(topic, json.dumps(payload), qos=1)
        
        with mid_to_uid_v2_lock:
            mid_to_uid_v2[name + "_" + str(msg.mid)] = (uid, t_sent)
        
        elapsed = 0
        interval = 0.1  # 100ms
        while elapsed < update_interval and not stop_event.is_set():
            time.sleep(interval)
            elapsed += interval

    client.loop_stop()
    client.disconnect()

def get_written_times_timescale(start_time):
    conn = psycopg2.connect(**PG_CONF)
    query = """
        SELECT 
            t.payload->>'uid' AS uid,
            t.ingested_at AS _time
        FROM thing_test_data t
        WHERE t.ingested_at >= %s
            AND t.payload ? 'uid';
    """
    
    with conn.cursor(cursor_factory=RealDictCursor) as cur:
        cur.execute(query, (start_time,))
        rows = cur.fetchall()

    conn.close()

    if not rows:
        raise ValueError("[ERROR] The query returned no results")
    
    df = pd.DataFrame(rows)
    df = df.dropna(subset=["uid"])
    df["uid"] = df["uid"].astype(str)
    df["_time"] = pd.to_datetime(df["_time"], utc=True)
    df = df.drop_duplicates(subset="uid", keep="last")
    df = df.set_index("uid")

    #print(df.head())

    return df

def run_test(num_devices, update_interval, test_duration, stop_event, wait_time):
    _sent_map_v2.clear()
    print("[OpenTwinsV2] Running test...")
    start_time = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S") + "Z"
    with ThreadPoolExecutor(max_workers=num_devices) as executor:
        futures = [
            executor.submit(simulate_device, f"device{i}", update_interval, test_duration, stop_event)
            for i in range(num_devices)
        ]
        for future in futures:
            if stop_event.is_set():
                break
            future.result()
    
    print("[OpenTwinsV2] Number of messages sent: " + str(len(_sent_map_v2)))
    print("[OpenTwinsV2] Waiting for data to appear in PostgreSQL...")
    time.sleep(wait_time)
    written_map = get_written_times_timescale(start_time)
    
    latencies = []
    lost_count = 0
    
    for uid, t_sent in _sent_map_v2.items():
        if uid in written_map.index:
            t_sent = parser.isoparse(t_sent)
            t_received = written_map.loc[uid, "_time"]
            if isinstance(t_received, pd.Series):
                t_received = t_received.iloc[0]
            #print(f"{uid} | SENT: {t_sent} ({t_sent.tzinfo}) | RECEIVED: {t_received} ({t_received.tzinfo}) | DIFF: {(t_received - t_sent).total_seconds()}")
            latencies.append((t_received - t_sent).total_seconds())
        else:
            lost_count += 1

    #print(latencies)

    return latencies, (lost_count / len(_sent_map_v2)) if len(_sent_map_v2) > 0 else 0
