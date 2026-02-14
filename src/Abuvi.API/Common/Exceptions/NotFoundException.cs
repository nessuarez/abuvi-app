namespace Abuvi.API.Common.Exceptions;

/// <summary>
/// Exception thrown when a requested resource is not found
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"No se encontró {entityName} con ID '{key}'")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}
