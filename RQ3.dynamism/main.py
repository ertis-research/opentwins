#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
RQ3 - Evaluating the dynamic updating and reconfiguration capabilities of the platform architecture.
"""

import logging
import os
from datetime import datetime, timezone
import time
from dotenv import load_dotenv
from kafka_client import KafkaClient
import matplotlib.pyplot as plt
import pandas as pd
from scenariosA import scenario_a1_a2_combined, scenario_a3_derived_property_propagation
from scenariosB import scenario_b1_create_thing_without_twin, scenario_b2_add_thing_to_twin, scenario_b3_delete_thing, scenario_b4_add_relationship, scenario_b5_modify_relationship, scenario_b6_delete_relationship

from mqtt import MQTTClient


# ========================================
# Configuration & Logging
# ========================================
load_dotenv()

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logging.getLogger("kafka").setLevel(logging.WARNING)

OUTPUT_CSV = "results_rq3.csv"


# ========================================
# Scenario Runner
# ========================================
def run_scenario_multiple_times(scenario_func, repetitions=5, mqtt_client=None, **kwargs):
    """
    Runs a scenario multiple times and returns a pandas DataFrame with results.
    """
    records = []

    for i in range(repetitions):
        logger.info(f"Run {i+1}/{repetitions} for {scenario_func.__name__}")
        match, latency = scenario_func(mqtt_client, **kwargs)
        records.append({
            "scenario": scenario_func.__name__,
            "iteration": i + 1,
            "match": match,
            "latency": latency,
            "timestamp": datetime.utcnow().isoformat()
        })

    df = pd.DataFrame(records)
    return df


# ========================================
# CSV Aggregation & Stats
# ========================================
def append_to_csv(df, path):
    if not os.path.exists(path):
        df.to_csv(path, index=False)
    else:
        df.to_csv(path, mode="a", index=False, header=False)
    logger.info(f"Results appended to {path}")


def summarize_results(csv_path):
    df = pd.read_csv(csv_path)
    summary = (
        df.groupby("scenario")
        .agg(
            avg_latency=("latency", "mean"),
            std_latency=("latency", "std"),
            success_rate=("match", "mean"),
            n=("latency", "count")
        )
        .reset_index()
    )
    logger.info(f"Summary:\n{summary}")
    return summary


# ========================================
# Visualization (paper-ready)
# ========================================
def plot_results(summary_df, output_dir):
    plt.style.use("seaborn-whitegrid")
    fig, ax1 = plt.subplots(figsize=(8, 5))

    # Bar plot for latency
    ax1.bar(summary_df["scenario"], summary_df["avg_latency"],
            yerr=summary_df["std_latency"], alpha=0.7, capsize=5)
    ax1.set_ylabel("Average Latency (s)", fontsize=12)
    ax1.set_xlabel("Scenario", fontsize=12)
    ax1.set_title("Performance Metrics per Scenario", fontsize=14)
    ax1.grid(True, linestyle="--", alpha=0.6)

    # Line plot for success rate
    # ax2 = ax1.twinx()
    # ax2.plot(summary_df["scenario"], summary_df["success_rate"] * 100, color="red",
    #             marker="o", linewidth=2, label="Success Rate")
    # ax2.set_ylabel("Success Rate (%)", color="red", fontsize=12)
    # ax2.tick_params(axis="y", labelcolor="red")
    # ax2.set_ylim(0, 100)

    plt.tight_layout()
    fig_path = os.path.join(output_dir, "rq3_results_plot.pdf")
    plt.savefig(fig_path, dpi=300)
    plt.show()
    logger.info(f"Saved plot to {fig_path}")
    

# ========================================
# Main Test Execution
# ========================================
def main():
    num_runs = 1
    
    mqtt_client = MQTTClient()
    kafka_client = KafkaClient()
    
    # Create results directory with timestamp
    timestamp = datetime.now().strftime("%Y%m%d_%H%M")
    output_dir = os.path.join("output", timestamp)
    os.makedirs(output_dir, exist_ok=True)
    results_csv = os.path.join(output_dir, OUTPUT_CSV)

    all_results = []

    #=== Run A1 + A2 as a joint block ===
    logger.info("Prewarn: Starting scenario A1 + A2 combined block...")
    scenario_a1_a2_combined(mqtt_client, kafka_client)
    for i in range(num_runs):
        logger.info(f"--- Iteration {i+1}/{num_runs} for A1+A2 block ---")
        df_a1a2 = scenario_a1_a2_combined(mqtt_client, kafka_client)
        df_a1a2["iteration"] = i + 1
        all_results.append(df_a1a2)

    # === Run A3 ===
    logger.info("Prewarn: Starting scenario A3 (Derived Property Propagation)...")
    scenario_a3_derived_property_propagation(mqtt_client)
    for i in range(num_runs):
        logger.info(f"--- Iteration {i+1}/{num_runs} for A3 ---")
        df_a3 = scenario_a3_derived_property_propagation(mqtt_client)
        df_a3["iteration"] = i + 1
        all_results.append(df_a3)
        
    #=== Run B1: Create thing without twin ===
    logger.info("Prewarn: Starting scenario B1 (Create Thing Without Twin)...")
    scenario_b1_create_thing_without_twin(kafka_client, output_dir)
    for i in range(num_runs):
        logger.info(f"--- Iteration {i+1}/{num_runs} for B1 (create thing without twin) ---")
        df_b1 = scenario_b1_create_thing_without_twin(kafka_client, output_dir)
        df_b1["iteration"] = i + 1
        all_results.append(df_b1)
    
    #=== Run B2: Add thing to twin ===
    logger.info("Prewarn: Starting scenario B2 (Add Thing to Twin)...")
    scenario_b2_add_thing_to_twin(output_dir)
    for i in range(num_runs):
        logger.info(f"--- Iteration {i+1}/{num_runs} for B2 (add thing to twin) ---")
        df_b2 = scenario_b2_add_thing_to_twin(output_dir)
        df_b2["iteration"] = i + 1
        all_results.append(df_b2)
    
    logger.info("Prewarn: Starting scenario B3 (Delete Thing)...")
    scenario_b3_delete_thing(kafka_client, output_dir)
    time.sleep(15)
    for i in range(num_runs):
        logger.info(f"--- Iteration {i+1}/{num_runs} for B3 (delete thing) ---")
        df_b3 = scenario_b3_delete_thing(kafka_client, output_dir)
        df_b3["iteration"] = i + 1
        all_results.append(df_b3)
        time.sleep(5)
    
    logger.info("Prewarn: Starting scenario B4 (Add Relationship)...")
    scenario_b4_add_relationship(kafka_client, output_dir)
    for i in range(num_runs):
        logger.info(f"--- Iteration {i+1}/{num_runs} for B4 (add relationship) ---")
        df_b4 = scenario_b4_add_relationship(kafka_client, output_dir)
        df_b4["iteration"] = i + 1
        all_results.append(df_b4)
    
    logger.info("Prewarn: Starting scenario B5 (Modify Relationship)...")
    scenario_b5_modify_relationship(kafka_client, output_dir)
    for i in range(num_runs):
        logger.info(f"--- Iteration {i+1}/{num_runs} for B5 (modify relationship) ---")
        df_b5 = scenario_b5_modify_relationship(kafka_client, output_dir)
        df_b5["iteration"] = i + 1
        all_results.append(df_b5)
    
    logger.info("Prewarn: Starting scenario B6 (Delete Relationship)...")
    scenario_b6_delete_relationship(kafka_client, output_dir)
    for i in range(num_runs):
        logger.info(f"--- Iteration {i+1}/{num_runs} for B6 (delete relationship) ---")
        df_b6 = scenario_b6_delete_relationship(kafka_client, output_dir)
        df_b6["iteration"] = i + 1
        all_results.append(df_b6)

    # Combine and save all
    df_all = pd.concat(all_results, ignore_index=True)
    append_to_csv(df_all, results_csv)

    summary = summarize_results(results_csv)
    plot_results(summary, output_dir)

    print("\n=== FINAL SUMMARY ===")
    print(summary.to_string(index=False))
    print(f"\nAll results saved to: {output_dir}")


if __name__ == "__main__":
    main()
