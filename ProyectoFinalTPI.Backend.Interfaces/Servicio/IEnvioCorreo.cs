using ProyectoFinalTPI.Backend.Dtos.Auth;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface IEnvioCorreo
{
    Task EnviarAsync(CorreoElectronicoDto correo, CancellationToken cancellationToken = default);
}

public interface IColaRecuperacionContrasena
{
    bool EncolarSolicitud(string email);
    bool EncolarAviso(string email, Guid recuperacionId);
}

// El worker usa este contrato; las operaciones HTTP siguen en IAuthServicio.
public interface IProcesadorRecuperacionContrasena
{
    Task<CorreoElectronicoDto?> PrepararRecuperacionAsync(string email, CancellationToken cancellationToken = default);
}
