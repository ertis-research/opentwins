# main.py

import signal
import threading
import opentwinsv1
import opentwinsv2
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
import sys
import ntplib

stop_event = threading.Event()

def signal_handler(sig, frame):
    print("[Main] Received Ctrl+C. Stopping...")
    stop_event.set()

signal.signal(signal.SIGINT, signal_handler)

SCENARIOS_BASELINE = [
    (10, 60.0, 10),     # 10 dispositivos, 1 msg/min, 10s
    (100, 60.0, 10),    # 100 dispositivos, 1 msg/min, 10s
    (100, 10.0, 10),    # 100 dispositivos, 1 msg/10s, 10s
    (1000, 10.0, 15),   # 1000 dispositivos, 1 msg/10s, 15s
]

SCENARIOS_MEDIUM_LOAD = [
    (1000, 1.0, 15),    # 1000 dispositivos, 1 msg/s, 15s
    (5000, 1.0, 20),    # 5000 dispositivos, 1 msg/s, 20s
    (10000, 1.0, 20),   # 10,000 dispositivos, 1 msg/s, 20s
    (10000, 0.1, 20),   # 10,000 dispositivos, 10 msg/s, 20s (ráfaga)
]

SCENARIOS_STRESS = [
    (20000, 1.0, 25),   # 20,000 dispositivos, 1 msg/s, 25s
    (50000, 1.0, 30),   # 50,000 dispositivos, 1 msg/s, 30s
    (100000, 1.0, 30),  # 100,000 dispositivos, 1 msg/s, 30s
    (100000, 0.1, 30),  # 100,000 dispositivos, 10 msg/s (burst), 30s
]

SCENARIOS = [
    (5, 1.0, 10),    # 5 dispositivos, 1 Hz, 10 segundos
    (10, 0.5, 10),   # 10 dispositivos, 2 Hz (0.5s), 10 segundos
    (20, 0.2, 10),   # 20 dispositivos, 5 Hz (0.2s), 10 segundos
    (50, 0.1, 10),   # 50 dispositivos, 10 Hz (0.1s), 10 segundos

    (100, 0.1, 15),  # 100 dispositivos, 10 Hz, 15 segundos
    (200, 0.05, 15), # 200 dispositivos, 20 Hz, 15 segundos
    #(500, 0.02, 20), # 500 dispositivos, 50 Hz, 20 segundos
    #(1000, 0.01, 20),# 1000 dispositivos, 100 Hz, 20 segundos

    #(1500, 0.01, 30),# 1500 dispositivos, 100 Hz, 30 segundos
    #(2000, 0.005, 30),# 2000 dispositivos, 200 Hz, 30 segundos
    #(3000, 0.002, 40),# 3000 dispositivos, 500 Hz, 40 segundos
    #(5000, 0.001, 60)# 5000 dispositivos, 1000 Hz, 60 segundos
]


labels = []
avg_latencies_v1 = []
avg_latencies_v2 = []
loss_rate_v1 = []
loss_rate_v2 = []

def get_ntp_offset():
    try:
        c = ntplib.NTPClient()
        response = c.request('ntp.ubuntu.com', version=3)
        offset = response.offset  # en segundos
        if abs(offset) > 0.05:
            print(f"[WARNING] Clock offset vs NTP exceeds threshold (0.05s): {offset:.6f} seconds")
        else:
            print(f"[Main] Clock offset vs NTP within acceptable range: {offset:.6f} seconds")
    except Exception as e:
        print(f"[WARNING] Failed to get NTP offset: {e}")

def run_scenario(num_devices, interval, duration, runs: int = 1):
    """
    Runs the same test scenario `runs` times and returns the average results.

    Parameters
    ----------
    num_devices : int
        Number of simulated devices.
    interval : float
        Message interval in seconds.
    duration : float
        Total duration of each test run in seconds.
    runs : int, optional
        Number of times to repeat the scenario (default is 5).
    """
    
    label = f"{num_devices}x@1/{interval:.0f}s"
    print(f"\n=== Scenario: {label} ({runs} runs) ===")

    get_ntp_offset()

    all_lat_v1, all_lat_v2 = [], []
    lossr_v1_runs, lossr_v2_runs = [], []

    opentwinsv1.prepare_test(num_devices, interval, duration)
    opentwinsv2.prepare_test(num_devices, interval, duration)

    for run in range(1, runs + 1):
        print(f"[Run {run}/{runs}] Running OpenTwinsV1...")
        lat_v1, lossr_v1 = opentwinsv1.run_test(num_devices, interval, duration, stop_event)
        all_lat_v1.extend(lat_v1)         # Accumulate all latency samples
        lossr_v1_runs.append(lossr_v1)    # Store run-level loss rate

        print(f"[Run {run}/{runs}] Running OpenTwinsV2...")
        lat_v2, lossr_v2 = opentwinsv2.run_test(num_devices, interval, duration, stop_event)
        all_lat_v2.extend(lat_v2)         # Accumulate all latency samples
        lossr_v2_runs.append(lossr_v2)    # Store run-level loss rate

    # Final statistics (overall averages)
    avg_v1 = np.mean(all_lat_v1)
    avg_v2 = np.mean(all_lat_v2)
    mean_lossr_v1 = np.mean(lossr_v1_runs)
    mean_lossr_v2 = np.mean(lossr_v2_runs)

    print(f"\n=== Average Results after {runs} runs ===")
    print(f"OpenTwinsV1 · Avg latency: {avg_v1:.4f} s · Avg loss: {mean_lossr_v1*100:.2f}%")
    print(f"OpenTwinsV2 · Avg latency: {avg_v2:.4f} s · Avg loss: {mean_lossr_v2*100:.2f}%")

    return label, avg_v1, mean_lossr_v1, avg_v2, mean_lossr_v2

def plot_latencies_summary(labels, lat_v1, lat_v2, name=""):
    x = np.arange(len(labels))
    width = 0.35

    plt.figure(figsize=(12, 6))
    plt.bar(x - width/2, lat_v1, width, label='OpenTwinsV1')
    plt.bar(x + width/2, lat_v2, width, label='OpenTwinsV2')
    plt.xticks(x, labels, rotation=45)
    plt.ylabel("End-to-End Latency (s)")
    plt.title("Latency from MQTT Publish to DB Write")
    plt.legend()
    plt.tight_layout()
    plt.savefig(name + "db_latency_comparison.png")
    plt.show()

def plot_loss_rate_summary(labels, loss_rate_v1, loss_rate_v2, name=""):
    x = np.arange(len(labels))
    width = 0.35

    plt.figure(figsize=(12, 6))
    plt.bar(x - width/2, loss_rate_v1, width, label='OpenTwinsV1', color='tomato')
    plt.bar(x + width/2, loss_rate_v2, width, label='OpenTwinsV2', color='steelblue')

    plt.xticks(x, labels, rotation=45)
    plt.ylabel("Loss Rate (%)")
    plt.title("Message Loss Rate Comparison Between Platforms")
    plt.legend()
    plt.tight_layout()
    plt.savefig(name + "loss_rate_comparison.png")
    plt.show()

def run_test(scenarios, name):
    for devices, interval, duration in scenarios:
        if stop_event.is_set():
            print("[Main] Stop event detected. Exiting loop.")
            break
        label, avg1, lrv1, avg2, lrv2 = run_scenario(devices, interval, duration)
        labels.append(label)
        avg_latencies_v1.append(avg1)
        avg_latencies_v2.append(avg2)
        loss_rate_v1.append(lrv1)
        loss_rate_v2.append(lrv2)

    plot_latencies_summary(labels, avg_latencies_v1, avg_latencies_v2, name)
    plot_loss_rate_summary(labels, loss_rate_v1, loss_rate_v2, name)

if __name__ == "__main__":
    try: 
        print("[Main] SCENARIOS BASELINE")
        run_test(SCENARIOS_BASELINE, "baseline_")
        
        print("[Main] SCENARIOS MEDIUM LOAD")
        run_test(SCENARIOS_MEDIUM_LOAD, "medium_load_")
        
        print("[Main] SCENARIOS STRESS")
        run_test(SCENARIOS_STRESS, "stress_")
        
    except KeyboardInterrupt:
        print("\n[Main] KeyboardInterrupt detected. Exiting gracefully.")
        stop_event.set()
        sys.exit(0)  # Fuerza salida inmediata
