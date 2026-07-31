[AttributeUsage(AttributeTargets.Method)]
public class SwaggerExampleAttribute : Attribute
{
    public string Example { get; }
    public SwaggerExampleAttribute(string example)
    {
        Example = example;
    }
}

[AttributeUsage(AttributeTargets.Parameter)]
public class SwaggerFormExampleAttribute : Attribute
{
    public string Example { get; }
    public SwaggerFormExampleAttribute(string example)
    {
        Example = example;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class SwaggerResponseExampleAttribute : Attribute
{
    public int StatusCode { get; }
    public string Example { get; }

    public SwaggerResponseExampleAttribute(int statusCode, string example)
    {
        StatusCode = statusCode;
        Example = example;
    }
}