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

import requests

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
                "type": "number",
                "forms": [
                    {
                    "href": f"https://example.com/things/test:device{num}/humidity",
                    "contentType": "application/json",
                    "op": ["readproperty"]
                    }
                ]
            },
            "temperature": {
                "title": "Temperature",
                "type": "number",
                "forms": [
                    {
                    "href": f"https://example.com/things/test:device{num}/temperature",
                    "contentType": "application/json",
                    "op": ["readproperty"]
                    }
                ]
            }
        },
        "links": [
        {
            "rel": "subscribeEvent",
            "href": f"https://example.com/things/mqtt/events/changes_device{num}",
            "type": "application/json"
        }],
        "otv2:rules": {
            "update": {
                "otv2:if": {
                    "==": [
                        {
                            "var": "eventName"
                        },
                        f"mqtt:changes_device{num}"
                    ]
                },
                "otv2:then": {
                    "otv2:updateState": {
                        "temperature": {
                            "value": {
                                "var": "payload.temperature"
                            }
                        },
                        "humidity": {
                            "value": {
                                "var": "payload.humidity"
                            }
                        }
                    },
                    "otv2:emitEvent": [
                    {
                        "event" : "thing.state.changed",
                        "data": "state" 
                    }
                ]
                }
            }
        }
    }

_sent_map = {}

def prepare_test(num_devices, update_interval, test_duration):
    print(f"[OpenTwinsV2] Preparing test with {num_devices} devices, interval {update_interval}s, duration {test_duration}s.")
    for i in range(num_devices):
        response = requests.post(OPENTWINSV2_CONF.get("things") + "/things", headers=headers, data=json.dumps(get_default_TD(str(i))))
        if response.status_code < 200 and response.status_code >= 300:
            print(f"[✗] Failed to initialize test:device{i}. Status code: {response.status_code}, Response: {response.text}")
    _sent_map.clear()

def generate_payload(uid):
    return {
        "uid": uid,
        "temperature": round(random.uniform(20.0, 30.0), 2),
        "humidity": round(random.uniform(40.0, 60.0), 2),
    }

def simulate_device(name, update_interval, test_duration, stop_event):
    client = mqtt.Client()
    client.connect(MQTT_CONF["host"], MQTT_CONF["port"])
    client.loop_start()

    start_time = time.time()
    topic = MQTT_CONF["topic"] + name
    
    payload = generate_payload(str(uuid4()))
    client.publish(topic, json.dumps(payload)) ## cold-start
    time.sleep(0.5)

    while time.time() - start_time < test_duration and not stop_event.is_set():
        uid = str(uuid4())
        t_sent = datetime.now(timezone.utc).isoformat()
        _sent_map[uid] = t_sent
        payload = generate_payload(uid)
        client.publish(topic, json.dumps(payload))
        
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

    df = pd.DataFrame(rows)
    df = df.dropna(subset=["uid"])
    df["uid"] = df["uid"].astype(str)
    df["_time"] = pd.to_datetime(df["_time"], utc=True)
    df = df.set_index("uid")

    #print(df.head())

    return df

def run_test(num_devices, update_interval, test_duration, stop_event):
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
    
    print("[OpenTwinsV2] Waiting for data to appear in PostgreSQL...")
    time.sleep(15)
    written_map = get_written_times_timescale(start_time)
    
    latencies = []
    lost_count = 0
    
    for uid, t_sent in _sent_map.items():
        if uid in written_map.index:
            t_sent = parser.isoparse(t_sent)
            t_received = written_map.loc[uid, "_time"]
            #print(f"{uid} | SENT: {t_sent} ({t_sent.tzinfo}) | RECEIVED: {t_received} ({t_received.tzinfo}) | DIFF: {(t_received - t_sent).total_seconds()}")
            latencies.append((t_received - t_sent).total_seconds())
        else:
            lost_count += 1

    #print(latencies)

    return latencies, (lost_count / len(_sent_map)) if len(_sent_map) > 0 else 0
