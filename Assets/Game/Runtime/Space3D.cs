using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Мост между spec-координатами и трёхмерной сценой.
    ///
    /// Вся математика игры — контур рельсов, центры клеток, точки выхода языка —
    /// написана в плоской системе спеки: начало в левом верхнем углу, Y вниз.
    /// Переписывать её ради объёма было бы глупо и опасно: она покрыта тестами.
    /// Поэтому плоскость спеки кладётся на XZ, а вверх уходит Y.
    ///
    ///   spec (x, y)  →  world (x * Scale, 0, -y * Scale)
    ///
    /// Ноль сцены совпадает с левым верхним углом макета 390x844, так что все числа
    /// из docs/unity-spec продолжают значить ровно то же самое.
    /// </summary>
    public static class Space3D
    {
        /// <summary>Единиц мира на пиксель макета. 0.1 даёт сцену примерно 39x84.</summary>
        public const float Scale = 0.1f;

        public static Vector3 ToWorld(Vector2 spec, float height = 0f)
            => new Vector3(spec.x * Scale, height, -spec.y * Scale);

        public static Vector3 ToWorld(float specX, float specY, float height = 0f)
            => new Vector3(specX * Scale, height, -specY * Scale);

        public static float Size(float specSize) => specSize * Scale;

        /// <summary>
        /// Поворот вокруг вертикали по углу касательной контура.
        /// В спеке угол отсчитывается в экранной системе, где Y вниз, поэтому
        /// на плоскости XZ он идёт с обратным знаком.
        /// </summary>
        public static Quaternion RotationFromSpecAngle(float specAngleDeg)
            => Quaternion.Euler(0f, specAngleDeg, 0f);

        /// <summary>Центр макета — точка, на которую смотрит камера.</summary>
        public static Vector3 LayoutCenter => ToWorld(195f, 422f);
    }
}
