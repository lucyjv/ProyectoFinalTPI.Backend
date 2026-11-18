namespace ProyectoFinalTPI.Backend.Servicio;

internal static class FotoPortadaCapsula
{
    internal const int MaximoBytes = 5 * 1024 * 1024;

    internal static async Task<byte[]> LeerYValidar(Stream archivo, string contentType, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(archivo);
        using var memoria = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = await archivo.ReadAsync(buffer, ct)) > 0)
        {
            if (memoria.Length + leidos > MaximoBytes) throw new ArgumentException("La portada no puede superar 5 MiB.");
            await memoria.WriteAsync(buffer.AsMemory(0, leidos), ct);
        }
        var bytes = memoria.ToArray();
        var tipo = bytes.Length >= 12 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff ? "image/jpeg"
            : bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? "image/webp"
            : null;
        if (tipo is null || !string.Equals(tipo, contentType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Elegí una foto JPEG, PNG o WebP válida. El archivo debe coincidir con su formato.");
        return bytes;
    }
}
