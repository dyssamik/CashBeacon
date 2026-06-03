namespace CashBeacon;

public static class Rk7Queries
{
    public static string GetPrintLayout(int code) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <RK7Query>
            <RK7Command CMD="GetPrintLayout" format="text">
                <Layout code="{code}" />
            </RK7Command>
        </RK7Query>
        """;

    public static string GetSystemInfo() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <RK7Query>
            <RK7Command CMD="GetSystemInfo" />
        </RK7Query>
        """;

    public static string GetOrderList() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <RK7Query>
            <RK7Command CMD="GetOrderList" />
        </RK7Query>
        """;
}