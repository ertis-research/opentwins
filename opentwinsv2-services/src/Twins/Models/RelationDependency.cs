using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Twins.Models
{
    public record RelationDependency
    (
        string RelName,
        bool Unidirectional,
        IEnumerable<ThingDependency> RelatedThings
    );
}