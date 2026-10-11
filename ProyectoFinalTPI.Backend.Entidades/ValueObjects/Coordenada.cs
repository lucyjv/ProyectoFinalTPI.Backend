namespace ProyectoFinalTPI.Backend.Entidades.ValueObjects
{
    /// <summary>
    /// Representa un par de coordenadas geográficas (latitud/longitud) válidas.
    /// Complementa el uso de NetTopologySuite.Geometries.Point en la entidad Lugar:
    /// se usa para validar los valores antes de construir el Point de PostGIS.
    /// </summary>
    public sealed class Coordenada : IEquatable<Coordenada>
    {
        public double Latitud { get; }
        public double Longitud { get; }

        public Coordenada(double latitud, double longitud)
        {
            if (latitud < -90.0 || latitud > 90.0)
                throw new ArgumentOutOfRangeException(
                    nameof(latitud),
                    $"La latitud debe estar entre -90 y 90. Se recibió: {latitud}.");

            if (longitud < -180.0 || longitud > 180.0)
                throw new ArgumentOutOfRangeException(
                    nameof(longitud),
                    $"La longitud debe estar entre -180 y 180. Se recibió: {longitud}.");

            Latitud = latitud;
            Longitud = longitud;
        }

        /// <summary>
        /// Convierte la coordenada al tipo Point de NetTopologySuite
        /// que usa la entidad Lugar para almacenarse en PostGIS.
        /// </summary>
        public NetTopologySuite.Geometries.Point APoint()
        {
            var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            return factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(Longitud, Latitud));
        }

        /// <summary>Construye una Coordenada directamente desde un Point de NTS.</summary>
        public static Coordenada DesdePunto(NetTopologySuite.Geometries.Point point)
        {
            if (point == null || point.IsEmpty)
                throw new ArgumentException("El punto no puede ser nulo o vacío.", nameof(point));
            // En NTS, X = Longitud, Y = Latitud
            return new Coordenada(latitud: point.Y, longitud: point.X);
        }

        public bool Equals(Coordenada? otro) =>
            otro is not null &&
            Math.Abs(Latitud - otro.Latitud) < 1e-9 &&
            Math.Abs(Longitud - otro.Longitud) < 1e-9;

        public override bool Equals(object? obj) => obj is Coordenada c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(Latitud, Longitud);
        public override string ToString() => $"({Latitud}, {Longitud})";

        public static bool operator ==(Coordenada? a, Coordenada? b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(Coordenada? a, Coordenada? b) => !(a == b);
    }
}
