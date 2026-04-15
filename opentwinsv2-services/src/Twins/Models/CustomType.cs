using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Twins.Services;
using VDS.Common.Collections.Enumerations;
using VDS.RDF;
using VDS.RDF.Update.Commands;

namespace Twins.Models
{
    public class CustomType(string prefix, string name, bool isType = false, bool isAttribute = false)
    {

        public class Connection((string Prefix, string Name) name, (string Prefix, string Name)? datatype = null)
        {
            public (string Prefix, string Name) Name = name; 
            public (string Prefix, string Name)? Datatype = datatype;

            public override string ToString()
            {
                return $"{Name.Prefix}:{Name.Name}{(Datatype is null ? "" : $"{Datatype?.Prefix}:{Datatype?.Name} ")}";
            }
        }

        public class Relation:Connection
        {
            public HashSet<(Connection Connection, bool IsBidirectional)> Objects;
            public bool IsDirect = true;

            public Relation((string Prefix, string Name) name, HashSet<(Connection, bool)> obj, bool isDirect = true) : base(name)
            {
                Objects = obj;
                IsDirect = isDirect;
            }

            public Relation((string Prefix, string Name) name) : base(name)
            {
                Objects = [];
            }

            public Relation((string Prefix, string Name) name, bool direct) : base(name)
            {
                Objects = [];
                IsDirect = direct;
            }

            public void AddObject(Connection c, bool isBid)
            {
                Objects.Add((c, isBid));
            }

            public void AddObjects(IEnumerable<(Connection con, bool isBid)> cons)
            {
                foreach(var (con, isBid) in cons)
                    Objects.Add((con, isBid));
            }

            public override string ToString()
            {
                var sb = new StringBuilder($"{(IsDirect ? "" : "Inverse ")}Relation {base.ToString()} -> [");
                foreach(var obj in Objects)
                    sb.Append($"{(obj.IsBidirectional ? "Bidirectional" : "Unidirectional")} {obj.Connection}, ");
                sb.Append(']');
                return sb.ToString();
            }
        }

        public class Constraint:Connection
        {
            public HashSet<Connection> Objects;

            public Constraint((string Prefix, string Name) name, HashSet<Connection> obj) : base(name)
            {
                Objects = obj;
            }

            public Constraint((string Prefix, string Name) name) : base(name)
            {
                Objects = [];
            }

            public void AddObject(Connection c)
            {
                Objects.Add(c);
            }

            public void AddObjects(IEnumerable<Connection> cons)
            {
                foreach(var c in cons)
                    Objects.Add(c);
            }

            public Constraint? GetInnerConstraint((string Prefix, string Name) name)
            {
                return Objects.Where(o => o is Constraint c && c.Name == (name.Prefix, name.Name)).Select(o => o as Constraint).FirstOrDefault();
            }

            public override string ToString()
            {
                var sb = new StringBuilder($"Constraint {base.ToString()} -> [");
                foreach(var obj in Objects)
                    sb.Append($"{obj}, ");
                sb.Append(']');
                return sb.ToString();
            }
        }

        public record NQuad(
            string Subject,
            string Predicate,
            string Object
        );

        public string Prefix = prefix;
        public string Name = name;
        public bool IsType = isType;
        public bool IsAttribute = isAttribute;
        public HashSet<(string Prefix, string Name)> Domain = [];
        public HashSet<(string Prefix, string Name)> Range = [];
        public Dictionary<(string Prefix, string Name), Relation> Relations = [];
        public Dictionary<(string Prefix, string Name), Constraint> Constraints = [];
        public HashSet<NQuad> NQuads = [];

        public void AddRelation(string prefixPredicate, string predicate, string prefixType, string type, string? prefixObj = null, string? obj = null, bool isBid = false)
        {
            (Relations.GetValueOrDefault((prefixPredicate, predicate)) ?? (Relations[(prefixPredicate, predicate)] = new Relation((prefixPredicate, predicate), []))).Objects.Add((prefixObj is null || obj is null ? new Connection((prefixType, type), ("", "uri")) : new Connection((prefixObj!, obj!) , (prefixType, type)), isBid));
        }

        public void AddRelation(string prefixPredicate, string predicate, Connection connection, bool isBid = false)
        {
            (Relations.GetValueOrDefault((prefixPredicate, predicate)) ?? (Relations[(prefixPredicate, predicate)] = new Relation((prefixPredicate, predicate), []))).Objects.Add((connection, isBid));
        }

        public void AddRelations(IEnumerable<Connection> relations)
        {
            foreach(var relation in relations)
                if(relation is Relation rel)
                    foreach(var objs in rel.Objects)
                        AddRelation(relation.Name.Prefix, relation.Name.Name, objs.Connection, objs.IsBidirectional);
        }

        public void AddConstraint(string prefixPredicate, string predicate, Connection connection)
        {
            var constraint = Constraints.GetValueOrDefault((prefixPredicate, predicate)) ?? (Constraints[(prefixPredicate, predicate)] = new Constraint((prefixPredicate, predicate), []));
            if(connection is Constraint cons)
                foreach(var objs in cons.Objects)
                    constraint.AddObject(objs);
            else
                constraint.Objects.Add(connection);
        }

        public void AddConstraints(IEnumerable<Connection> constraints)
        {
            foreach(var constraint in constraints)
                if(constraint is Constraint)
                    AddConstraint(constraint.Name.Prefix, constraint.Name.Name, constraint);
        }

        public Constraint? GetConstraint((string Prefix, string Name) name)
        {
            return Constraints.GetValueOrDefault(name);
        }

        public void AddDomainUri((string uriPrefix, string uriName) uri)
        {
            Domain.Add(uri);
        }

        public void AddRangeUri((string uriPrefix, string uriName) uri)
        {
            Range.Add(uri);
        }

        public void AddNQuad(NQuad nq)
        {
            NQuads.Add(nq);
        }

        public void AddNQuads(IEnumerable<NQuad> nqs)
        {
            foreach(var nq in nqs)
                AddNQuad(nq);
        }

        public void IsAClass()
        {
            IsType=true;
            IsAttribute=false;
        }

        public void IsAnAttribute()
        {
            IsType=false;
            IsAttribute = true;
        }

        public void IsARelation()
        {
            IsType=false;
            IsAttribute=false;
        }

        public static void MergeIntoCurrent(CustomType info, Connection? nest, IEnumerable<Connection> newConnections, bool intoRelation, INode subj, IGraph graph)
        {
            if(nest is null)
                if(intoRelation)
                    info.AddRelations(newConnections);
                else
                    info.AddConstraints(newConnections);
            else if(nest is Relation nestedRel)
                nestedRel.AddObjects(newConnections.Select(c => (c, FormatService.IsRelationBidirectional(subj, nestedRel.Name.Prefix, nestedRel.Name.Name, c.Name.Prefix, c.Name.Name, graph))));
            else if(nest is Constraint nestedConst)
                nestedConst.AddObjects(newConnections);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Prefix}:{Name}");
            sb.AppendLine(IsType ? "Type" : (IsAttribute ? "Attribute" : "Relation"));
            sb.Append("Domain: ");
            foreach(var domain in Domain)
                sb.Append($"{domain}, ");
            sb.AppendLine();
            sb.Append("Range: ");
            foreach(var range in Range)
                sb.Append($"{range}, ");
            sb.AppendLine();
            sb.AppendLine($"Constraints:");
            foreach(var rel in Constraints)
                sb.AppendLine(rel.Value.ToString());
            sb.AppendLine($"Relations:");
            foreach(var rel in Relations)
                sb.AppendLine(rel.Value.ToString());
            return sb.ToString();
        }

        
    }
}