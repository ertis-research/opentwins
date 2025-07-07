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

_sent_map = {}

def prepare_test(num_devices, update_interval, test_duration):
    print(f"[OpenTwinsV1] Preparing test with {num_devices} devices, interval {update_interval}s, duration {test_duration}s.")
    for i in range(num_devices):
        try:
            response = requests.put(EXTENDED_CONF["url"] + "/api/twins/test:device" + str(i), headers=headers, data=json.dumps(default_thing), timeout=5)
            if response.status_code < 200 and response.status_code >= 300:
                print(f"[ERROR] Failed to initialize test:device{i}. Status code: {response.status_code}, Response: {response.text}")
        except requests.exceptions.Timeout:
            print(f"[ERROR] Timeout while initializing test:device{i}")
        except requests.exceptions.RequestException as e:
            print(f"[ERROR] Error while initializing test:device{i}: {e}")
    _sent_map.clear()


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

def simulate_device(name, update_interval, test_duration, stop_event):
    client = mqtt.Client()
    client.connect(MQTT_CONF["host"], MQTT_CONF["port"])
    client.loop_start()

    start_time = time.time()
    
    payload = generate_ditto_protocol(name, str(uuid4()))
    client.publish(MQTT_CONF["topic"], json.dumps(payload)) ## cold-start
    time.sleep(0.5)

    while time.time() - start_time < test_duration and not stop_event.is_set():
        uid = str(uuid4())
        t_sent = datetime.now(timezone.utc).isoformat()
        _sent_map[uid] = t_sent
        payload = generate_ditto_protocol(name, uid)
        client.publish(MQTT_CONF["topic"], json.dumps(payload))
        
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
    df = df.set_index("correlationId")

    return df

def run_test(num_devices, update_interval, test_duration, stop_event):
    _sent_map.clear()
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
    
    print("[OpenTwinsV1] Waiting for data to appear in InfluxDB...")
    time.sleep(15)
    written_map = get_written_times_influx(start_time)
    
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
