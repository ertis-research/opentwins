using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Twins.Models
{
    public record ThingDependency(
        string SrcThingId,
        IEnumerable<string> TypeDependency = default!,
        IEnumerable<RelationDependency> Dependencies = default!
    );
}