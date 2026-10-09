namespace ProyectoFinalTPI.Backend.Entidades.ValueObjects
{
    /// <summary>
    /// Representa una fecha de efeméride: una fecha recurring anual definida por mes y día,
    /// sin año específico (ej: "el 25 de mayo", "el 2 de abril").
    /// Se usa para agrupar publicaciones por fechas históricas relevantes.
    /// </summary>
    public sealed class FechaEfemeride : IEquatable<FechaEfemeride>
    {
        public int Mes { get; }
        public int Dia { get; }

        public FechaEfemeride(int mes, int dia)
        {
            if (mes < 1 || mes > 12)
                throw new ArgumentOutOfRangeException(nameof(mes),
                    $"El mes debe estar entre 1 y 12. Se recibió: {mes}.");

            // Validamos el día contra el mes usando un año bisiesto de referencia
            // para aceptar el 29 de febrero como efeméride válida.
            int diasEnMes = DateTime.DaysInMonth(year: 2000, month: mes);
            if (dia < 1 || dia > diasEnMes)
                throw new ArgumentOutOfRangeException(nameof(dia),
                    $"El día {dia} no es válido para el mes {mes} (máximo: {diasEnMes}).");

            Mes = mes;
            Dia = dia;
        }

        /// <summary>Construye una FechaEfemeride ignorando el año de un DateTime.</summary>
        public static FechaEfemeride DesdeFecha(DateTime fecha) =>
            new FechaEfemeride(fecha.Month, fecha.Day);

        /// <summary>
        /// Indica si una fecha dada cae en esta efeméride (mismo mes y día, cualquier año).
        /// </summary>
        public bool Coincide(DateTime fecha) => fecha.Month == Mes && fecha.Day == Dia;

        /// <summary>Devuelve la próxima ocurrencia de esta efeméride desde hoy.</summary>
        public DateTime ProximaOcurrencia()
        {
            var hoy = DateTime.UtcNow.Date;
            var esteAño = new DateTime(hoy.Year, Mes, Dia);
            return esteAño >= hoy ? esteAño : esteAño.AddYears(1);
        }

        public bool Equals(FechaEfemeride? otro) =>
            otro is not null && Mes == otro.Mes && Dia == otro.Dia;

        public override bool Equals(object? obj) => obj is FechaEfemeride f && Equals(f);
        public override int GetHashCode() => HashCode.Combine(Mes, Dia);
        public override string ToString() => $"{Dia:D2}/{Mes:D2}";

        public static bool operator ==(FechaEfemeride? a, FechaEfemeride? b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(FechaEfemeride? a, FechaEfemeride? b) => !(a == b);
    }
}
