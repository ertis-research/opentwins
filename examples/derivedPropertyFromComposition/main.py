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

# Dos tópicos distintos
topic_device1 = "telemetry/powerdevice1"
topic_device2 = "telemetry/powerdevice2"

# Crear cliente MQTT
mqttc = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)

# Conectar al broker
mqttc.connect(broker, port)

def generate_data_device1():
    return {
        "power": round(random.uniform(50.0, 150.0), 2),
        "timestamp": int(time.time())
    }

def generate_data_device2():
    return {
        "power": round(random.uniform(40.0, 120.0), 2),
        "timestamp": int(time.time())
    }

print(f"Publishing events every minute on:\n - {topic_device1}\n - {topic_device2}")

try:
    while True:
        # Generar datos para cada dispositivo
        payload1 = json.dumps(generate_data_device1())
        payload2 = json.dumps(generate_data_device2())

        # Publicar en cada tópico
        mqttc.publish(topic_device1, payload1)
        mqttc.publish(topic_device2, payload2)

        print(f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] Published to {topic_device1}: {payload1}")
        print(f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] Published to {topic_device2}: {payload2}")

        time.sleep(30)  # esperar 1 minuto
except KeyboardInterrupt:
    print("Stopped by user.")
finally:
    mqttc.disconnect()
