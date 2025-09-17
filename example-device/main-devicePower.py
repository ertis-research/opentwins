import time
import json
import os
import random
import paho.mqtt.client as mqtt
from dotenv import load_dotenv

# Cargar variables de entorno
load_dotenv()

broker = os.getenv("MQTT_BROKER", "localhost")
port = int(os.getenv("MQTT_PORT", 1883))
topic = "telemetry/devices_power"

# Crear cliente MQTT
mqttc = mqtt.Client()

# Conectar al broker
mqttc.connect(broker, port)

def generate_data():
    return {
        "device1Power": round(random.uniform(50.0, 150.0), 2),
        "device2Power": round(random.uniform(40.0, 120.0), 2),
        "timestamp": int(time.time())
    }

print(f"Publishing events every minute on {topic}...")

try:
    while True:
        payload = json.dumps(generate_data())
        mqttc.publish(topic, payload)
        print(f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] Published: {payload}")
        time.sleep(60)
except KeyboardInterrupt:
    print("Stopped by user.")
finally:
    mqttc.disconnect()
