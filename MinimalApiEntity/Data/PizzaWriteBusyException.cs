namespace MinimalApiEntity.Data;

public sealed class PizzaWriteBusyException(Exception innerException)
    : Exception("Timed out waiting for the shared pizza write lock.", innerException);
