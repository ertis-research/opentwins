query_assoc = """
PREFIX ex: <http://example.org/airport#>

SELECT ?airport (COUNT(DISTINCT ?gate) AS ?totalGates) (COUNT(DISTINCT ?gateWithPlane) AS ?occupiedGates)
WHERE {
    ?airport a ex:Airport ;
             ex:hasChild ?terminal .
    ?terminal ex:hasChild ?gate .
    OPTIONAL {
        ?gate ex:hasPlane ?plane .
        BIND(?gate AS ?gateWithPlane)
    }
}
GROUP BY ?airport
HAVING (?totalGates = ?occupiedGates)
"""