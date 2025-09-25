query_p2p = """
PREFIX ex: <http://example.org/airport#>

SELECT ?planeA ?planeB
WHERE {
  ?planeA a ex:Plane .
  ?planeB a ex:Plane .
  ?planeA ex:conflictsWith ?planeB .
}
"""