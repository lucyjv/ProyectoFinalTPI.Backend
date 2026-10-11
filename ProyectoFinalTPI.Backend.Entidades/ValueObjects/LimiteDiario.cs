namespace ProyectoFinalTPI.Backend.Entidades.ValueObjects
{
    /// <summary>
    /// Representa el límite diario de publicaciones que puede hacer un usuario.
    /// Encapsula la regla de negocio: cuántas publicaciones se permiten por día,
    /// y si el usuario ya alcanzó ese límite dado un conteo actual.
    /// </summary>
    public sealed class LimiteDiario : IEquatable<LimiteDiario>
    {
        public int Maximo { get; }

        // Límite por defecto para usuarios comunes
        public static readonly LimiteDiario Estandar = new LimiteDiario(10);

        // Límite ampliado para usuarios de nivel alto (Cronista o Leyenda)
        public static readonly LimiteDiario Premium = new LimiteDiario(30);

        // Sin límite: para moderadores y administradores
        public static readonly LimiteDiario Ilimitado = new LimiteDiario(int.MaxValue);

        public LimiteDiario(int maximo)
        {
            if (maximo <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximo),
                    $"El límite diario debe ser mayor a cero. Se recibió: {maximo}.");

            Maximo = maximo;
        }

        /// <summary>
        /// Determina el límite que corresponde según el nivel del usuario.
        /// </summary>
        public static LimiteDiario ParaNivel(NivelCategoria nivel) => nivel.Numero switch
        {
            >= 4 => Premium,
            _    => Estandar
        };

        /// <summary>Indica si el usuario ya alcanzó su límite diario.</summary>
        public bool AlcanzadoPor(int publicacionesHoy)
        {
            if (publicacionesHoy < 0)
                throw new ArgumentOutOfRangeException(nameof(publicacionesHoy),
                    "Las publicaciones del día no pueden ser negativas.");

            return publicacionesHoy >= Maximo;
        }

        /// <summary>Cuántas publicaciones le quedan disponibles hoy.</summary>
        public int RestantesPara(int publicacionesHoy)
        {
            if (Maximo == int.MaxValue) return int.MaxValue;
            return Math.Max(0, Maximo - publicacionesHoy);
        }

        public bool EsIlimitado => Maximo == int.MaxValue;

        public bool Equals(LimiteDiario? otro) => otro is not null && Maximo == otro.Maximo;
        public override bool Equals(object? obj) => obj is LimiteDiario l && Equals(l);
        public override int GetHashCode() => Maximo.GetHashCode();
        public override string ToString() => EsIlimitado ? "Sin límite" : $"{Maximo} publicaciones/día";

        public static bool operator ==(LimiteDiario? a, LimiteDiario? b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(LimiteDiario? a, LimiteDiario? b) => !(a == b);
    }
}
