query_hierarchy = """
PREFIX ex: <http://example.org/airport#>

SELECT ?airport (COUNT(DISTINCT ?gate) AS ?totalGates) (COUNT(DISTINCT ?occupied) AS ?occupiedGates)
WHERE {
  ?airport a ex:Airport ;
           ex:hasChild ?terminal .
  ?terminal ex:hasChild ?gate .
  OPTIONAL {
    ?plane ex:relatedTo ?occupied .
    FILTER(?occupied = ?gate)
  }
}
GROUP BY ?airport
HAVING (?totalGates = ?occupiedGates)
"""