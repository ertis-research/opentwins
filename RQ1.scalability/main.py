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
import asyncio
import os
import csv
import figure

stop_event = threading.Event()

def signal_handler(sig, frame):
    print("[Main] Received Ctrl+C. Stopping...")
    stop_event.set()

signal.signal(signal.SIGINT, signal_handler)

SCENARIOS_BASELINE = [
    (10, 60.0, 10),     # 10 dispositivos, 1 msg/min, 10s
    (100, 30.0, 10),    # 100 dispositivos, 1 msg/30s, 10s
    (500, 10.0, 10),    # 500 dispositivos, 1 msg/10s, 10s
    (1000, 10.0, 15),   # 1000 dispositivos, 1 msg/10s, 15s
]

SCENARIOS_MEDIUM_LOAD = [
    (1000, 2.0, 20),     # 1000 dispositivos, 1 msg/2s (~500 msg/s), 20s
    (2500, 1.0, 20),     # 2500 dispositivos, 1 msg/s, 20s
    (5000, 1.0, 20),     # 5000 dispositivos, 1 msg/s (~5k msg/s), 20s
    (5000, 0.2, 15),     # 5000 dispositivos, 5 msg/s (~25k msg/s), 15s
]


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


def save_results_to_csv(filename, labels, lat_v1, loss_rate_v1, lat_v2, loss_rate_v2):
    output_dir = "output"
    os.makedirs(output_dir, exist_ok=True)  # Asegura que la carpeta output exista

    filepath = os.path.join(output_dir, filename)
    with open(filepath, mode='w', newline='') as file:
        writer = csv.writer(file)
        writer.writerow(['Label', 'Latency_v1', 'LossRate_v1', 'Latency_v2', 'LossRate_v2'])
        for label, l1, l1_loss, l2, l2_loss in zip(labels, lat_v1, loss_rate_v1, lat_v2, loss_rate_v2):
            writer.writerow([label, l1, l1_loss, l2, l2_loss])


async def run_scenario(num_devices, interval, duration, wait_time, runs: int = 1):
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
    
    label = f"{num_devices}x@1/{int(interval) if interval.is_integer() else f'{interval:.2f}'}s"
    print(f"\n=== Scenario: {label} ({runs} runs) ===")

    get_ntp_offset()

    all_lat_v1, all_lat_v2 = [], []
    lossr_v1_runs, lossr_v2_runs = [], []

    await opentwinsv1.prepare_test(num_devices, interval, duration)
    await opentwinsv2.prepare_test(num_devices, interval, duration)

    for run in range(1, runs + 1):
        print(f"[Run {run}/{runs}] Running OpenTwinsV1...")
        lat_v1, lossr_v1 = opentwinsv1.run_test(num_devices, interval, duration, stop_event, wait_time)
        print(f"[Run {run}/{runs}] Latency: {np.mean(lat_v1):.4f} · Loss rate: {lossr_v1*100:.2f}%")
        all_lat_v1.extend(lat_v1)         # Accumulate all latency samples
        lossr_v1_runs.append(lossr_v1)    # Store run-level loss rate

        print(f"[Run {run}/{runs}] Running OpenTwinsV2...")
        lat_v2, lossr_v2 = opentwinsv2.run_test(num_devices, interval, duration, stop_event, wait_time)
        print(f"[Run {run}/{runs}] Latency: {np.mean(lat_v2):.4f} · Loss rate: {lossr_v2*100:.2f}%")
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

def run_test(scenarios, name, wait_time):
    # Reiniciamos las listas al empezar la función
    labels = []
    avg_latencies_v1 = []
    avg_latencies_v2 = []
    loss_rate_v1 = []
    loss_rate_v2 = []
    
    for devices, interval, duration in scenarios:
        if stop_event.is_set():
            print("[Main] Stop event detected. Exiting loop.")
            break
        label, avg1, lrv1, avg2, lrv2 = asyncio.run(run_scenario(devices, interval, duration, wait_time))
        labels.append(label)
        avg_latencies_v1.append(avg1)
        avg_latencies_v2.append(avg2)
        loss_rate_v1.append(lrv1)
        loss_rate_v2.append(lrv2)

    #plot_latencies_summary(labels, avg_latencies_v1, avg_latencies_v2, name)
    #plot_loss_rate_summary(labels, loss_rate_v1, loss_rate_v2, name)
    
    save_results_to_csv(f"{name}.csv", labels, avg_latencies_v1, loss_rate_v1, avg_latencies_v2, loss_rate_v2)

if __name__ == "__main__":
    try: 
        print("[Main] SCENARIOS BASELINE")
        run_test(SCENARIOS_BASELINE, "baseline", 15)
        figure.generate_plots_from_csv("baseline.csv", "Baseline")
        
        print("[Main] SCENARIOS INCREASED LOAD")
        run_test(SCENARIOS_MEDIUM_LOAD, "increased_load", 45)
        figure.generate_plots_from_csv("increased_load.csv", "Increased Load")

        
    except KeyboardInterrupt:
        print("\n[Main] KeyboardInterrupt detected. Exiting gracefully.")
        stop_event.set()
        sys.exit(0)  # Fuerza salida inmediata
