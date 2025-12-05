using Google.Cloud.Firestore;

namespace Firebase.Models.WhatsApp;

/// <summary>
/// Modelo que almacena el estado de conversaci�n de un usuario de WhatsApp.
/// </summary>
[FirestoreData]
public class WhatsAppSession
{
    /// <summary>
    /// ID de la sesi�n (n�mero de tel�fono del usuario, ej: "5491112345678")
    /// </summary>
    [FirestoreProperty]
    public required string Id { get; set; }

    /// <summary>
    /// ID del cliente asociado (null si a�n no est� registrado)
    /// </summary>
    [FirestoreProperty]
    public string? ClienteId { get; set; }

    /// <summary>
    /// Estado actual del flujo conversacional
    /// </summary>
    [FirestoreProperty]
    public required string CurrentState { get; set; }

    /// <summary>
    /// Datos temporales capturados durante el flujo (ej: nombre, apellido, etc.)
    /// </summary>
    [FirestoreProperty]
    public Dictionary<string, string> TemporaryData { get; set; } = new();

    /// <summary>
    /// �ltima interacci�n del usuario
    /// </summary>
    [FirestoreProperty]
    public DateTime LastInteraction { get; set; }

    /// <summary>
    /// Fecha de creaci�n de la sesi�n
    /// </summary>
    [FirestoreProperty]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Indica si el usuario est� autenticado (tiene ClienteId)
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(ClienteId);
}

/// <summary>
/// Estados posibles del flujo conversacional
/// </summary>
public static class WhatsAppFlowStates
{
    // Estados de registro de cliente
    public const string REGISTRO_TIPO_DOCUMENTO = "REGISTRO_TIPO_DOCUMENTO";
    public const string REGISTRO_NUM_DOCUMENTO = "REGISTRO_NUM_DOCUMENTO";
    public const string REGISTRO_NOMBRE = "REGISTRO_NOMBRE";
    public const string REGISTRO_APELLIDO = "REGISTRO_APELLIDO";
    public const string REGISTRO_EMAIL = "REGISTRO_EMAIL";
    public const string REGISTRO_CONFIRMACION = "REGISTRO_CONFIRMACION";

    // Estados de gesti�n de veh�culos
    public const string VEHICULO_MENU = "VEHICULO_MENU";
    public const string VEHICULO_TIPO = "VEHICULO_TIPO";
    public const string VEHICULO_PATENTE = "VEHICULO_PATENTE";
    public const string VEHICULO_MARCA = "VEHICULO_MARCA";
    public const string VEHICULO_MODELO = "VEHICULO_MODELO";
    public const string VEHICULO_COLOR = "VEHICULO_COLOR";
    public const string VEHICULO_CONFIRMACION = "VEHICULO_CONFIRMACION";

    // Estados de consulta
    public const string MENU_CLIENTE_AUTENTICADO = "MENU_CLIENTE_AUTENTICADO";
    public const string MOSTRAR_DATOS = "MOSTRAR_DATOS";
    
    // Estados de edici�n de cliente
    public const string EDITAR_DATOS_MENU = "EDITAR_DATOS_MENU";
    public const string EDITAR_NOMBRE = "EDITAR_NOMBRE";
    public const string EDITAR_APELLIDO = "EDITAR_APELLIDO";
    public const string EDITAR_EMAIL = "EDITAR_EMAIL";
    public const string CONFIRMAR_EDICION = "CONFIRMAR_EDICION";

    // Estados de gesti�n de veh�culos
    public const string MENU_VEHICULOS = "MENU_VEHICULOS";
    public const string MOSTRAR_VEHICULOS = "MOSTRAR_VEHICULOS";
    public const string SELECCIONAR_VEHICULO_MODIFICAR = "SELECCIONAR_VEHICULO_MODIFICAR";
    public const string MODIFICAR_VEHICULO_MENU = "MODIFICAR_VEHICULO_MENU";
    public const string MODIFICAR_VEHICULO_MODELO = "MODIFICAR_VEHICULO_MODELO";
    public const string MODIFICAR_VEHICULO_COLOR = "MODIFICAR_VEHICULO_COLOR";
    public const string CONFIRMAR_ELIMINAR_VEHICULO = "CONFIRMAR_ELIMINAR_VEHICULO";

    // Estados de asociación de vehículos (múltiples dueños)
    public const string ASOCIAR_VEHICULO_PATENTE = "ASOCIAR_VEHICULO_PATENTE";
    public const string ASOCIAR_VEHICULO_CLAVE = "ASOCIAR_VEHICULO_CLAVE";
    public const string ASOCIAR_VEHICULO_CONFIRMACION = "ASOCIAR_VEHICULO_CONFIRMACION";
    public const string MOSTRAR_CLAVE_VEHICULO = "MOSTRAR_CLAVE_VEHICULO";

    // Estado para seleccionar opción de vehículo durante registro inicial
    public const string REGISTRO_VEHICULO_OPCION = "REGISTRO_VEHICULO_OPCION";

    // Estado inicial
    public const string INICIO = "INICIO";
}
