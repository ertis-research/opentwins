import csv
import os
from matplotlib import pyplot as plt
import numpy as np


def plot_latencies_summary(labels, lat_v1, lat_v2, name=""):
    x = np.arange(len(labels))
    width = 0.4
    short_name = name.lower().replace(" ", "_")

    plt.figure(figsize=(8, 8))
    plt.bar(x - width/2, lat_v1, width, label='OpenTwinsV1', color='darkcyan')
    plt.bar(x + width/2, lat_v2, width, label='OpenTwinsV2', color='darkorange')
    plt.xticks(x, labels, rotation=45, fontsize=12, fontweight='bold')
    plt.yticks(fontsize=12, fontweight='bold')
    plt.ylabel("End-to-End Latency (s)", fontsize=12, fontweight='bold')
    plt.title(name + " - Latency from MQTT Publish to DB Write", fontsize=13, fontweight='bold')
    legend = plt.legend(fontsize=12)
    for text in legend.get_texts():
        text.set_fontweight('bold')
    plt.tight_layout()
    plt.savefig("output/" +short_name + "_db_latency_comparison.pdf")
    #plt.show()

def plot_loss_rate_summary(labels, loss_rate_v1, loss_rate_v2, name=""):
    x = np.arange(len(labels))
    width = 0.4
    short_name = name.lower().replace(" ", "_")

    # Convertir a porcentaje
    loss_rate_v1 = np.array(loss_rate_v1) * 100
    loss_rate_v2 = np.array(loss_rate_v2) * 100

    plt.figure(figsize=(8, 8))
    plt.bar(x - width/2, loss_rate_v1, width, label='OpenTwinsV1', color='darkcyan')
    plt.bar(x + width/2, loss_rate_v2, width, label='OpenTwinsV2', color='darkorange')

    plt.xticks(x, labels, rotation=45, fontsize=12, fontweight='bold')
    plt.yticks(fontsize=12, fontweight='bold')
    plt.ylabel("Loss Rate (%)", fontsize=12, fontweight='bold')
    plt.title(name + " - Message Loss Rate Comparison Between Platforms", fontsize=13, fontweight='bold')
    legend = plt.legend(fontsize=12)
    for text in legend.get_texts():
        text.set_fontweight('bold')
    plt.tight_layout()
    plt.savefig("output/" + short_name + "_loss_rate_comparison.pdf")
    #plt.show()

def generate_plots_from_csv(filename, name):
    # Leer los resultados desde el archivo CSV
    filepath = os.path.join('output', filename)
    labels = []
    lat_v1 = []
    lat_v2 = []
    loss_rate_v1 = []
    loss_rate_v2 = []

    with open(filepath, mode='r') as file:
        reader = csv.reader(file)
        next(reader)  # Saltar la cabecera
        for row in reader:
            labels.append(row[0])
            lat_v1.append(float(row[1]))
            loss_rate_v1.append(float(row[2]))
            lat_v2.append(float(row[3]))
            loss_rate_v2.append(float(row[4]))

    # Generar los gráficos
    plot_latencies_summary(labels, lat_v1, lat_v2, name)
    plot_loss_rate_summary(labels, loss_rate_v1, loss_rate_v2, name)