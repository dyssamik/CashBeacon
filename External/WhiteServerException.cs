namespace CashBeacon;

public class WhiteServerException : Exception
{
	public WhiteServerException(string message)
		: base(message) { }

	public WhiteServerException(string message, Exception inner)
		: base(message, inner) { }
}

public class WhiteServerOfflineException : WhiteServerException
{
	public WhiteServerOfflineException(string message)
		: base(message) { }

	public WhiteServerOfflineException(string message, Exception inner)
		: base(message, inner) { }
}