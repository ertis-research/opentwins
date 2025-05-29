# dispositivo_mqtt.py
import time
import json
import os
import random
import paho.mqtt.client as mqtt
from dotenv import load_dotenv

load_dotenv()

broker = os.getenv("MQTT_BROKER", "localhost")
port = int(os.getenv("MQTT_PORT", 1883))
topic = "example-device"

mqttc = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)

mqttc.connect(broker, port)

def generate_data():
    return {
        "temperature": round(random.uniform(20.0, 30.0), 2),
        "timestamp": int(time.time())
    }

while True:
    payload = json.dumps(generate_data())
    mqttc.publish(topic, payload)
    print(f"Publicado: {payload}")
    time.sleep(5)