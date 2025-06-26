# main.py

import signal
import threading
import opentwinsv1
import opentwinsv2
import matplotlib.pyplot as plt
import numpy as np
import sys

stop_event = threading.Event()

def signal_handler(sig, frame):
    print("[Main] Received Ctrl+C. Stopping...")
    stop_event.set()

signal.signal(signal.SIGINT, signal_handler)

SCENARIOS = [
    (5, 1.0, 10),    # 5 dispositivos, 1 Hz, 10 segundos
    (10, 0.5, 10),   # 10 dispositivos, 2 Hz (0.5s), 10 segundos
    (20, 0.2, 10),   # 20 dispositivos, 5 Hz (0.2s), 10 segundos
    (50, 0.1, 10),   # 50 dispositivos, 10 Hz (0.1s), 10 segundos

    (100, 0.1, 15),  # 100 dispositivos, 10 Hz, 15 segundos
    (200, 0.05, 15), # 200 dispositivos, 20 Hz, 15 segundos
    (500, 0.02, 20), # 500 dispositivos, 50 Hz, 20 segundos
    (1000, 0.01, 20),# 1000 dispositivos, 100 Hz, 20 segundos

    (1500, 0.01, 30)# 1500 dispositivos, 100 Hz, 30 segundos
    #(2000, 0.005, 30),# 2000 dispositivos, 200 Hz, 30 segundos
    #(3000, 0.002, 40),# 3000 dispositivos, 500 Hz, 40 segundos
    #(5000, 0.001, 60),# 5000 dispositivos, 1000 Hz, 60 segundos
]

labels = []
avg_latencies_v1 = []
avg_latencies_v2 = []

def run_scenario(num_devices, interval, duration):
    label = f"{num_devices}x@{1/interval:.1f}Hz"
    print(f"\n=== Scenario: {label} ===")

    opentwinsv1.prepare_test(num_devices, interval, duration)
    #opentwinsv2.prepare_test(num_devices, interval, duration)

    print("[Main] Running OpenTwinsV1...")
    lat_v1 = opentwinsv1.run_test(num_devices, interval, duration, stop_event)
    avg_v1 = np.mean(lat_v1)
    print(f"[Main] OpenTwinsV1 avg latency: {avg_v1:.4f} s")


    avg_v2 = None
    # print("[Main] Running OpenTwinsV2...")
    # lat_v2 = opentwinsv2.run_test(num_devices, interval, duration)
    # avg_v2 = np.mean(lat_v2)
    # print(f"[Main] OpenTwinsV2 avg latency: {avg_v2:.4f} s")

    return label, avg_v1, avg_v2

def plot_summary(labels, lat_v1, lat_v2):
    x = np.arange(len(labels))
    width = 0.35

    plt.figure(figsize=(12, 6))
    plt.bar(x - width/2, lat_v1, width, label='OpenTwinsV1 (InfluxDB)')
    plt.bar(x + width/2, lat_v2, width, label='OpenTwinsV2 (TimescaleDB)')
    plt.xticks(x, labels, rotation=45)
    plt.ylabel("End-to-End Latency (s)")
    plt.title("Latency from MQTT Publish to DB Write")
    plt.legend()
    plt.tight_layout()
    plt.savefig("db_latency_comparison.png")
    plt.show()

if __name__ == "__main__":
    try: 
        for devices, interval, duration in SCENARIOS:
            if stop_event.is_set():
                print("[Main] Stop event detected. Exiting loop.")
                break
            label, avg1, avg2 = run_scenario(devices, interval, duration)
            #labels.append(label)
            #avg_latencies_v1.append(avg1)
            #avg_latencies_v2.append(avg2)

        #plot_summary(labels, avg_latencies_v1, avg_latencies_v2)
    except KeyboardInterrupt:
        print("\n[Main] KeyboardInterrupt detected. Exiting gracefully.")
        stop_event.set()
        sys.exit(0)  # Fuerza salida inmediata
