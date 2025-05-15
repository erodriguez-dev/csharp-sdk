using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace QuickstartWeatherServer.Tools;

[McpServerToolType]
public sealed class WeatherTools
{
    [McpServerTool, Description("Get weather alerts for a US state.")]
    public static async Task<string> GetAlerts(
        HttpClient client,
        [Description("The US state to get alerts for. Use the 2 letter abbreviation for the state (e.g. NY).")] string state)
    {
        using var jsonDocument = await client.ReadJsonDocumentAsync($"/alerts/active/area/{state}");
        var jsonElement = jsonDocument.RootElement;
        var alerts = jsonElement.GetProperty("features").EnumerateArray();

        if (!alerts.Any())
        {
            return "No active alerts for this state.";
        }

        return string.Join("\n--\n", alerts.Select(alert =>
        {
            JsonElement properties = alert.GetProperty("properties");
            return $"""
                    Event: {properties.GetProperty("event").GetString()}
                    Area: {properties.GetProperty("areaDesc").GetString()}
                    Severity: {properties.GetProperty("severity").GetString()}
                    Description: {properties.GetProperty("description").GetString()}
                    Instruction: {properties.GetProperty("instruction").GetString()}
                    """;
        }));
    }

    [McpServerTool, Description("Get weather forecast for a location.")]
    public static async Task<string> GetForecast(
        HttpClient client,
        [Description("Latitude of the location.")] double latitude,
        [Description("Longitude of the location.")] double longitude)
    {
        var pointUrl = string.Create(CultureInfo.InvariantCulture, $"/points/{latitude},{longitude}");
        using var jsonDocument = await client.ReadJsonDocumentAsync(pointUrl);
        var forecastUrl = jsonDocument.RootElement.GetProperty("properties").GetProperty("forecast").GetString()
            ?? throw new Exception($"No forecast URL provided by {client.BaseAddress}points/{latitude},{longitude}");

        using var forecastDocument = await client.ReadJsonDocumentAsync(forecastUrl);
        var periods = forecastDocument.RootElement.GetProperty("properties").GetProperty("periods").EnumerateArray();

        return string.Join("\n---\n", periods.Select(period => $"""
                {period.GetProperty("name").GetString()}
                Temperature: {period.GetProperty("temperature").GetInt32()}°F
                Wind: {period.GetProperty("windSpeed").GetString()} {period.GetProperty("windDirection").GetString()}
                Forecast: {period.GetProperty("detailedForecast").GetString()}
                """));
    }

    [McpServerTool, Description("Envío de un correo electrónico.")]
    public static async Task<string> SendEmail(
    [Description("Dirección de correo electrónico del destinatario.")] string to,
    [Description("Asunto del correo.")] string subject,
    [Description("Cuerpo del mensaje.")] string body)
    {
        var fechaEnvio = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var mensaje = $"Correo enviado exitosamente a {to} el {fechaEnvio}.\nAsunto: {subject}";
        return mensaje;
    }

    [McpServerTool, Description("Obtiene información de los transportes registrados, con opciones de filtrado por nombre, código o estado.")]
    public static async Task<string> GetTransports(
    [Description("Texto de búsqueda para filtrar por nombre o código. Opcional.")] string? search = null,
    [Description("Filtrar solo transportes activos. Por defecto: true.")] bool onlyActive = true)
    {
        // Crear el cliente HTTP aquí (o usar un helper estático si ya tienes uno centralizado)
        using var client = new HttpClient();
        client.BaseAddress = new Uri("https://gocodeart.com");

        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");
        var url = "/backend/api/v1.0/business/transports";
        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        using var jsonDocument = await client.ReadJsonDocumentAsync(url);
        var transportsArray = jsonDocument.RootElement.EnumerateArray().ToList();

        IEnumerable<JsonElement> transportsFiltered = transportsArray;

        if (onlyActive)
            transportsFiltered = transportsArray.Where(t => t.GetProperty("is_active").GetBoolean());

        if (!transportsFiltered.Any())
            return "No se encontraron transportes que coincidan con los criterios especificados.";

        return string.Join("\n---\n", transportsFiltered.Select(t =>
            $"""
        Código: {t.GetProperty("code").GetString()}
        Nombre: {t.GetProperty("name").GetString()}
        RIF: {t.GetProperty("tax_identification").GetString()}
        Estado: {(t.GetProperty("is_active").GetBoolean() ? "Activo" : "Inactivo")}
        """));
    }

    [McpServerTool, Description("Obtiene las liquidaciones recientes de viajes. Permite filtrar por ID de transporte.")]
    public static async Task<string> GetRecentLiquidations(
    [Description("ID del transporte para filtrar liquidaciones. Si es null, devuelve todas.")] int? idTransport = null)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri("https://gocodeart.com");

        var url = "/backend/api/v1.0/logistics/liquidations/recent";
        using var jsonDocument = await client.ReadJsonDocumentAsync(url);
        var liquidationsArray = jsonDocument.RootElement.EnumerateArray().ToList();

        // Filtrado opcional por idTransport
        IEnumerable<JsonElement> filteredLiquidations = liquidationsArray;
        if (idTransport.HasValue)
            filteredLiquidations = liquidationsArray.Where(l => l.GetProperty("id_transport").GetInt32() == idTransport.Value);

        if (!filteredLiquidations.Any())
            return "No se encontraron liquidaciones para los criterios especificados.";

        // Preparar salida legible
        return string.Join("\n=====\n", filteredLiquidations.Select(l =>
            $"""
        Lote: {l.GetProperty("liquidation_batch_id").GetInt32()}
        Transporte: {l.GetProperty("transport_name").GetString()}
        Subtotal: {l.GetProperty("subtotal").GetDecimal():N2} {l.GetProperty("currency").GetString()}
        Total: {l.GetProperty("total").GetDecimal():N2} {l.GetProperty("currency").GetString()}
        Estado: {l.GetProperty("status").GetString()}
        Fecha: {l.GetProperty("liquidation_date").GetString()}
        Detalles:
        {string.Join("\n", l.GetProperty("details").EnumerateArray().Select(d =>
                $"  Ruta: {d.GetProperty("route_name").GetString()} | Monto: {d.GetProperty("applied_amount").GetDecimal():N2} | Regla: {d.GetProperty("calculation_details").GetString()}"))}
        """));
    }



}
