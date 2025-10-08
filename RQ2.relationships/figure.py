import matplotlib.pyplot as plt
import networkx as nx
import numpy as np
from rdflib import Graph, Literal
import os
import random

def visualize_all_graphs_paper_ready():
    """
    Paper-ready visualization of six RDF graphs (esc1.ttl ... esc6.ttl).

    Scenarios:
    1–2: Hierarchical Airport→Terminal→Gate, show occupied.value inside nodes.
    3–4: Tree + planes near gates, keep otv2:hasChild and location edges.
    5–6: Show ONLY planes (no edges), each once, with flying.value below each node in random layout.
    """

    fig, axes = plt.subplots(2, 3, figsize=(18, 10), facecolor="white")
    axes = axes.flatten()
    filenames = [f"knowledgeGraphs/esc{i}.ttl" for i in range(1, 7)]

    ignore_keywords = ["createdAt", "lastUpdate"]
    name_predicate = "http://example.org/name"

    def get_color(uri):
        if "Airport" in uri:
            return "#fd6060"
        elif "Terminal" in uri:
            return "#ffd46f"
        elif "Gate" in uri:
            return "#8DB1D1"
        elif "Plane" in uri:
            return "#73ceb3"
        else:
            return "#90be6d"

    # hierarchical layout for trees
    def hierarchy_pos(G, root=None, width=0.9, vert_gap=0.2, vert_loc=0, xcenter=0.3):
        pos = {}
        if root is None:
            root = next(iter(nx.topological_sort(G))) if nx.is_directed_acyclic_graph(G) else list(G.nodes)[0]
        children = list(G.successors(root))
        if not children:
            pos[root] = (xcenter, vert_loc)
        else:
            dx = width / len(children)
            nextx = xcenter - width / 2 - dx / 2
            for child in children:
                nextx += dx
                pos.update(hierarchy_pos(G, child, width=dx, vert_gap=vert_gap,
                                            vert_loc=vert_loc - vert_gap, xcenter=nextx))
            pos[root] = (xcenter, vert_loc)
        return pos

    for i, fname in enumerate(filenames):
        ax = axes[i]
        ax.set_facecolor("white")

        if not os.path.exists(fname):
            ax.text(0.5, 0.5, "No data", ha="center", va="center")
            ax.set_xticks([])  # Eliminar marcas del eje X
            ax.set_yticks([])  # Eliminar marcas del eje Y
            continue

        g = Graph()
        g.parse(fname, format="turtle")

        node_labels = {}
        node_inner_text = {}

        # Extract labels and values
        for s, p, o in g:
            p_str = str(p)
            if p_str == name_predicate and isinstance(o, Literal):
                node_labels[str(s)] = str(o)
            elif isinstance(o, Literal):
                if i in [0, 1] and p_str.endswith("occupied.value"):
                    node_inner_text[str(s)] = f"occupied: {o}"
                elif i in [4, 5] and p_str.endswith("flying.value"):
                    node_inner_text[str(s)] = f"flying: {o}"

        G_nx = nx.DiGraph()
        for s, p, o in g:
            p_str = str(p)
            if any(kw in p_str for kw in ignore_keywords):
                continue
            if isinstance(o, Literal):
                continue
            # skip edges for 5–6
            if i in [4, 5]:
                continue
            G_nx.add_edge(str(s), str(o), label=p_str)

        # Scenarios 5–6 → only unique planes, random layout
        if i in [4, 5]:
            # Filter only plane nodes (Plane1 to Plane5)
            plane_nodes = sorted(
                set(str(n) for n in g.subjects() if "Plane" in str(n))
            )
            
            # Ensure that we only get planes 1 to 5
            plane_nodes = [p for p in plane_nodes if "Plane" in p]
            plane_nodes = sorted(plane_nodes)  # Keep them ordered by name for clarity

            G_nx = nx.DiGraph()
            for n in plane_nodes:
                G_nx.add_node(n)

            pos = nx.circular_layout(G_nx, scale=0.5)
            ax.set_xlim(-1, 1)
            ax.set_ylim(-1, 1)

        elif i in [0, 1]:
            pos = hierarchy_pos(G_nx)
            pos_values = np.array(list(pos.values()))
            ax.set_xlim(pos_values[:, 0].min() - 0.1, pos_values[:, 0].max() + 0.1)
            ax.set_ylim(pos_values[:, 1].min() - 0.1, pos_values[:, 1].max() + 0.08)
        elif i in [2, 3]:
            for subj in g.subjects():
                subj_str = str(subj)
                if "Plane" in subj_str and subj_str not in G_nx.nodes:
                    G_nx.add_node(subj_str)
            core_nodes = [n for n in G_nx.nodes if "Plane" not in n]
            subG = G_nx.subgraph(core_nodes)
            pos_core = hierarchy_pos(subG)
            pos = dict(pos_core)
            lost_nodes = 0
            
            airport_node = next((n for n in pos_core.keys() if "Airport" in n), None)
            if airport_node:
                ax_airport, ay_airport = pos_core[airport_node]
            else:
                # fallback por si no hay airport en el grafo
                ax_airport, ay_airport = (0.0, 0.0)
            
            plane_nodes = [n for n in G_nx.nodes if "Plane" in n]
            for node in plane_nodes:
                print(node)
                linked_gate = None
                for u, v in G_nx.edges:
                    if v == node and "Gate" in u:
                        linked_gate = u
                        break
                    elif u == node and "Gate" in v:
                        linked_gate = v
                        break
                if linked_gate and linked_gate in pos_core:
                    gx, gy = pos_core[linked_gate]
                    pos[node] = (gx, gy - 0.15)
                else:
                    right = -1
                    if(lost_nodes%2 == 0):
                        right = 1
                    offset_x = random.uniform(-0.05, 0.05)
                    offset_y = random.uniform(-0.05, 0)
                    pos[node] = (ax_airport + (0.30 * right) + offset_x, ay_airport + offset_y)
                    lost_nodes = lost_nodes + 1
                        
            pos_values = np.array(list(pos.values()))
            ax.set_xlim(pos_values[:, 0].min() - 0.1, pos_values[:, 0].max() + 0.1)
            ax.set_ylim(pos_values[:, 1].min() - 0.1, pos_values[:, 1].max() + 0.1)
        else:
            pos = nx.spring_layout(G_nx, seed=42)

        node_colors = [get_color(n) for n in G_nx.nodes]
        labels_to_draw = {n: node_labels.get(n, n.split(":")[-1]) for n in G_nx.nodes}

        # Edge labels
        edge_labels_raw = nx.get_edge_attributes(G_nx, 'label')
        edge_labels = {}
        for (u, v), label in edge_labels_raw.items():
            short = (
                label.replace("http://example.org/", "")
                     .replace("http://example.org/otv2:", "otv2:")
            )
            if i in [0, 1]:
                edge_labels[(u, v)] = short
            elif i in [2, 3]:
                if "otv2:hasChild" in short or "location" in short:
                    edge_labels[(u, v)] = short

        # Draw
        nx.draw_networkx_nodes(G_nx, pos, ax=ax,
                                node_color=node_colors, linewidths=0.8, node_size=1800, edgecolors="black")
        if i not in [4, 5]:
            nx.draw_networkx_edges(G_nx, pos, ax=ax,
                                    arrows=True, arrowstyle="->",
                                    width=0.8, alpha=0.7)
            nx.draw_networkx_edge_labels(G_nx, pos, edge_labels=edge_labels,
                                            font_size=7, label_pos=0.5,
                                            rotate=False, ax=ax, font_weight="bold")
        nx.draw_networkx_labels(G_nx, pos, labels=labels_to_draw,
                                font_size=7, ax=ax, font_weight='bold')

        # Inner text (occupied / flying)
        if i not in [4, 5]:
            for n, (x, y) in pos.items():
                if n in node_inner_text:
                    ax.text(x, y - 0.05, node_inner_text[n],
                            fontsize=7, ha="center", va="center",
                            color="black", fontweight='bold')
        else:
            for n, (x, y) in pos.items():
                if n in node_inner_text:
                    ax.text(x, y - 0.18, node_inner_text[n],
                            fontsize=7, ha="center", va="center",
                            color="black", fontweight='bold')

        ax.set_title(f"Scenario {i+1}", fontsize=15, pad=5, fontweight='bold')
        
        for spine in ax.spines.values():  # Establecer borde negro
            spine.set_edgecolor('black')
            spine.set_linewidth(2)
        
        ax.set_xticks([])  # Eliminar marcas del eje X
        ax.set_yticks([])  # Eliminar marcas del eje Y

    plt.tight_layout()
    plt.subplots_adjust(wspace=0.10, hspace=0.10)
    plt.savefig("rq2_graphs_paper.pdf", dpi=600,
                bbox_inches="tight", facecolor="white")
    #plt.show()
    print("[INFO] Image 'rq2_graphs_paper.pdf' successfully generated.")
