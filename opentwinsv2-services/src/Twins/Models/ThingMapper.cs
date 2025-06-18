using OpenTwinsv2.Things.Models;

namespace OpenTwinsv2.Twins.Models
{
    public static class ThingMapper
    {
        public static ThingNode? MapToThingNode(ThingDescription td)
        {
            return new ThingNode
            {
                Name = !string.IsNullOrWhiteSpace(td.Title)
                    ? td.Title
                    : td.Titles?.Values.FirstOrDefault() ?? "Unnamed Thing",

                ThingId = td.Id,
                /*
                                Types = td.TypeAnnotation?
                                    .Select(type => new ThingNode { Name = type })
                                    .ToList() ?? [],
                */
                Events = td.Events?
                    .Select(kvp => new EventNode { Name = kvp.Key })
                    .ToList() ?? [],

                // Twins: se deja vacío por defecto, puedes completarlo si sabes cómo se relacionan
            };
        }
    }
}