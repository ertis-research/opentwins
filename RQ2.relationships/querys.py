from rdflib import Graph
import datetime

TRACE_FILE = "output/proof_trace.log"

def log_trace(message):
    """Append trace messages to file and print to console."""
    timestamp = datetime.datetime.now().strftime("%H:%M:%S")
    with open(TRACE_FILE, "a") as f:
        f.write(f"[{timestamp}] {message}\n")
    print(f"[TRACE] {message}")

def check(g: Graph, query: str, rule_name: str, rule_description: str):
    """Run ASK query, evaluate result, and log inference trace."""
    log_trace(f"Applying rule: {rule_name} - {rule_description}")
    log_trace("Running SPARQL query...")
    res = g.query(query)

    if bool(res):
        result = "NOT COLLAPSED"
        log_trace("Condition satisfied: Exists element not meeting collapse criteria.")
    else:
        result = "COLLAPSED"
        log_trace("Condition not satisfied: Collapse criteria met.")

    print(f"Result: {result}")
    log_trace(f"Inference conclusion: {result}\n")
    return result


def check_hierarchy(g: Graph):
    query = """
    PREFIX ns1: <http://example.org/otv2:>
    PREFIX ns2: <http://example.org/>

    ASK {
        VALUES ?airport { <urn:test:rq2:Airport1> }

        ?airport ns1:hasChild+ ?gate .
        ?gate ns2:name ?gateName .

        FILTER EXISTS {
            ?gate ns2:occupied.value ?status .
            FILTER(?status = false)
        }
    }
    """
    check(
        g,
        query,
        rule_name="R1 - Hierarchical collapse rule",
        rule_description="IF all gates of the airport are occupied THEN airport collapsed",
    )


def check_associative(g: Graph):
    query = """
    PREFIX ns1: <http://example.org/>
    PREFIX ns2: <http://example.org/otv2:>

    ASK {
        ?gate ns1:name ?name .
        FILTER regex(str(?gate), "Gate", "i")
        FILTER NOT EXISTS { ?plane ns1:location ?gate }
    }
    """
    check(
        g,
        query,
        rule_name="R2 - Associative collapse rule",
        rule_description="IF all gates have planes assigned THEN airport collapsed",
    )
    
    
def check_p2p(g: Graph):
    query = """
    PREFIX ns1: <http://example.org/>

    ASK {
    {
        SELECT (COUNT(DISTINCT ?plane) AS ?planesFlying)
        WHERE {
            ?plane ns1:flying.value true .
        }
    }
    FILTER(?planesFlying >= 2)
    }
    """
    check(
        g,
        query,
        rule_name="R3 - Peer-to-peer collapse rule",
        rule_description="IF fewer than 2 planes are flying THEN airport collapsed",
    )