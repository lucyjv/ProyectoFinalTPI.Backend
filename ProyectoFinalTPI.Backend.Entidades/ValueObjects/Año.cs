namespace ProyectoFinalTPI.Backend.Entidades.ValueObjects
{
    /// <summary>
    /// Representa un año válido dentro del dominio de la aplicación.
    /// Garantiza que el año esté en el rango histórico que la plataforma soporta (1900 - año actual).
    /// </summary>
    public sealed class Año : IEquatable<Año>
    {
        public int Valor { get; }

        private static readonly int AñoMinimo = 1909;
        private static readonly int AñoMaximo = DateTime.UtcNow.Year;

        public Año(int valor)
        {
            if (valor < AñoMinimo || valor > AñoMaximo)
                throw new ArgumentOutOfRangeException(
                    nameof(valor),
                    $"El año debe estar entre {AñoMinimo} y {AñoMaximo}. Se recibió: {valor}.");

            Valor = valor;
        }

        /// <summary>Crea un Año a partir de un DateTime, usando solo su componente Year.</summary>
        public static Año DesdeFecha(DateTime fecha) => new Año(fecha.Year);

        /// <summary>Devuelve la década a la que pertenece este año (ej: 1987 → Decada(1980)).</summary>
        public Decada ADecada() => new Decada(Valor - (Valor % 10));

        public bool Equals(Año? otro) => otro is not null && Valor == otro.Valor;
        public override bool Equals(object? obj) => obj is Año a && Equals(a);
        public override int GetHashCode() => Valor.GetHashCode();
        public override string ToString() => Valor.ToString();

        public static bool operator ==(Año? a, Año? b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(Año? a, Año? b) => !(a == b);
    }
}
