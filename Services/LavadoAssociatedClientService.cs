using Firebase.Models;
using FirebaseLoginCustom.Controllers;

namespace Firebase.Services;

public class LavadoAssociatedClientService
{
    private readonly ClienteService _clienteService;
    private readonly VehiculoService _vehiculoService;
    private readonly TipoDocumentoService _tipoDocumentoService;

    public LavadoAssociatedClientService(ClienteService clienteService, VehiculoService vehiculoService, TipoDocumentoService tipoDocumentoService)
    {
        _clienteService = clienteService;
        _vehiculoService = vehiculoService;
        _tipoDocumentoService = tipoDocumentoService;
    }

    public record Resultado(Cliente? Cliente = null, string? Error = null);

    public async Task<Resultado> Registrar(LavadosController.RegistroClienteAsociadoRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.VehiculoId) ||
            string.IsNullOrWhiteSpace(request.ClaveAsociacion))
            return new(Error: "Selecciona un vehículo e ingresa su clave de asociación.");

        if (new[] { request.TipoDocumento, request.NumeroDocumento, request.Nombre, request.Apellido, request.Telefono, request.Email }
            .Any(string.IsNullOrWhiteSpace))
            return new(Error: "Completa todos los campos obligatorios.");

        var vehiculo = await _vehiculoService.ObtenerVehiculo(request.VehiculoId);
        if (vehiculo == null || vehiculo.Estado != "Activo")
            return new(Error: "El vehículo seleccionado ya no está disponible.");

        if (string.IsNullOrEmpty(vehiculo.ClaveAsociacionHash) ||
            !VehiculoService.ValidarClaveAsociacion(request.ClaveAsociacion, vehiculo.ClaveAsociacionHash))
            return new(Error: "La clave de asociación no es válida para este vehículo.");

        var tipoDocumento = await _tipoDocumentoService.ObtenerTipoDocumentoPorNombre(request.TipoDocumento.Trim());
        if (tipoDocumento == null || string.IsNullOrWhiteSpace(tipoDocumento.Formato))
            return new(Error: "Selecciona un tipo de documento válido.");

        bool documentoValido;
        try
        {
            documentoValido = System.Text.RegularExpressions.Regex.IsMatch(
                request.NumeroDocumento?.Trim() ?? string.Empty,
                tipoDocumento.ObtenerRegexPattern());
        }
        catch (ArgumentException)
        {
            documentoValido = false;
        }
        if (!documentoValido)
            return new(Error: $"El número de documento debe respetar el formato: {tipoDocumento.Formato}.");

        var existente = await _clienteService.ObtenerClientePorDocumento(request.TipoDocumento, request.NumeroDocumento);
        if (existente != null)
            return new(Error: "Ya existe un cliente con este documento.");

        var cliente = new Cliente
        {
            Id = string.Empty,
            TipoDocumento = request.TipoDocumento.Trim(),
            NumeroDocumento = request.NumeroDocumento.Trim(),
            Nombre = request.Nombre.Trim(),
            Apellido = request.Apellido.Trim(),
            Telefono = request.Telefono.Trim(),
            Email = request.Email.Trim(),
            Estado = "Activo",
            VehiculosIds = new List<string> { vehiculo.Id }
        };

        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(cliente);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        foreach (var propertyName in new[] { nameof(Cliente.TipoDocumento), nameof(Cliente.Nombre), nameof(Cliente.Apellido), nameof(Cliente.Telefono), nameof(Cliente.Email) })
        {
            var value = typeof(Cliente).GetProperty(propertyName)!.GetValue(cliente);
            var propertyContext = new System.ComponentModel.DataAnnotations.ValidationContext(cliente) { MemberName = propertyName };
            System.ComponentModel.DataAnnotations.Validator.TryValidateProperty(value, propertyContext, validationResults);
        }
        if (validationResults.Count > 0)
            return new(Error: string.Join(" ", validationResults.Select(result => result.ErrorMessage)));

        var asociado = await _clienteService.CrearClienteAsociado(cliente, vehiculo.Id, request.ClaveAsociacion);
        if (!asociado)
            return new(Error: "No se pudo asociar el cliente al vehículo.");

        return new(cliente);
    }
}
