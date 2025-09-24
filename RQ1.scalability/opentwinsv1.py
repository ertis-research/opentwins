import time, json, random
from datetime import datetime, timezone
from uuid import uuid4
import paho.mqtt.client as mqtt
from concurrent.futures import ThreadPoolExecutor, as_completed
from influxdb_client import InfluxDBClient
from dateutil import parser
import pandas as pd
import requests
import warnings
from influxdb_client.client.warnings import MissingPivotFunction
from dotenv import load_dotenv
import os
import httpx
import traceback
import asyncio
import threading

warnings.simplefilter("ignore", MissingPivotFunction)

load_dotenv()

EXTENDED_CONF = {
    "url": os.getenv("EXTENDED_URL")
}

MQTT_CONF = {
    "host": os.getenv("MQTT_V1_HOST"),
    "port": int(os.getenv("MQTT_V1_PORT", 1883)),
    "topic": os.getenv("MQTT_V1_TOPIC")
}

INFLUX_CONF = {
    "url": os.getenv("INFLUX_URL"),
    "org": os.getenv("INFLUX_ORG"),
    "token" : os.getenv("INFLUX_TOKEN")
}

headers = {
    "Content-Type": "application/json",
    # "Authorization": "Bearer tu_token"
}

default_thing =  {
    "attributes": {},
    "features": {
        "humidity": {
            "properties": {
                "value": None
            }
        },
        "temperature": {
            "properties": {
                "value": None
            }
        }
    }
}

_sent_map_v1 = {}
_sent_map_v1_lock = threading.Lock()

mid_to_uid = {}
mid_to_uid_lock = threading.Lock()

async def prepare_test(num_devices, update_interval, test_duration):
    print(f"[OpenTwinsV1] Preparing test with {num_devices} devices, interval {update_interval}s, duration {test_duration}s.")
    
    semaphore = asyncio.Semaphore(100)  # limita a 50 peticiones concurrentes
    
    async with httpx.AsyncClient(timeout=15) as client:
        async def put_device(i):
            device_name = f"test:device{i}"
            url = EXTENDED_CONF["url"] + f"/api/twins/{device_name}"
            async with semaphore:
                try:
                    response = await client.put(url, headers=headers, json=default_thing)
                    if not (200 <= response.status_code < 300):
                        print(f"[ERROR] Failed to initialize test:device{i}. Status code: {response.status_code}, Response: {response.text}")
                except Exception as e:
                    print(f"[ERROR] Error initializing test:device{i}: {repr(e)}")
        
        tasks = [put_device(i) for i in range(num_devices)]
        await asyncio.gather(*tasks)

    _sent_map_v1.clear()


def generate_ditto_protocol(name, uid):
    return {
        "topic": f"test/{name}/things/twin/commands/merge",
        "headers": {
            "content-type": "application/merge-patch+json",
            "correlation-id": uid
        },
        "path": "/features",
        "value": {
            "humidity": {
                "properties": {
                    "value": round(random.uniform(40.0, 60.0), 2)
                }
            },
            "temperature": {
                "properties":{
                    "value": round(random.uniform(20.0, 30.0), 2)
                }
            }
        }
    }

def on_publish(client, userdata, mid):
    full_mid = userdata["name"] + "_" + str(mid)
    
    uid = None
    t_sent = None
    
    with mid_to_uid_lock:
        value = mid_to_uid.pop(full_mid, None)
        if value:
            uid, t_sent = value
        
    if uid and t_sent:
        with _sent_map_v1_lock:
            _sent_map_v1[uid] = t_sent
    #else:
        #print(f"[WARNING] Published mid={full_mid}, but uid not found") #Cold start no guarda UID asi que siempre encuentra mal eso
        


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
    
    # Cold-start
    payload = generate_ditto_protocol(name, str(uuid4()))
    client.publish(MQTT_CONF["topic"], json.dumps(payload)) 
    time.sleep(1)

    while time.time() - start_time < test_duration and not stop_event.is_set():
        uid = str(uuid4())
        t_sent = datetime.now(timezone.utc).isoformat()
        
        payload = generate_ditto_protocol(name, uid)
        msg = client.publish(topic, json.dumps(payload), qos=1)
        
        with mid_to_uid_lock:
            mid_to_uid[name + "_" + str(msg.mid)] = (uid, t_sent)
        
        elapsed = 0
        interval = 0.1  # 100ms
        while elapsed < update_interval and not stop_event.is_set():
            time.sleep(interval)
            elapsed += interval

    client.loop_stop()
    client.disconnect()

def get_written_times_influx(start_time):
    client = InfluxDBClient(url=INFLUX_CONF["url"], token=INFLUX_CONF["token"], org=INFLUX_CONF["org"])
    query_api = client.query_api()
    query = f'''
    from(bucket: "default")
        |> range(start: {start_time})
        |> filter(fn: (r) => r["_measurement"] == "mqtt_consumer")
        |> filter(fn: (r) => r["_field"] == "value_temperature_properties_value")
        |> filter(fn: (r) => r["thingId"] =~ /test:device/)  
        |> keep(columns: ["_time", "correlationId"])
    '''
    df = query_api.query_data_frame(query)
    df = df.drop(columns=["result", "table"])
    df["correlationId"] = df["correlationId"].astype("string")
    df["_time"] = pd.to_datetime(df["_time"], utc=True)
    df = df.drop_duplicates(subset="correlationId", keep="last")
    df = df.set_index("correlationId")

    return df

def run_test(num_devices, update_interval, test_duration, stop_event, wait_time):
    _sent_map_v1.clear()
    print("[OpenTwinsV1] Running test...")
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
    
    print("[OpenTwinsV1] Number of messages sent: " + str(len(_sent_map_v1)))
    print("[OpenTwinsV1] Waiting for data to appear in InfluxDB...")
    time.sleep(int(wait_time))
    written_map = get_written_times_influx(start_time)
    
    latencies = []
    lost_count = 0
    
    for uid, t_sent in _sent_map_v1.items():
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

    return latencies, (lost_count / len(_sent_map_v1)) if len(_sent_map_v1) > 0 else 0
