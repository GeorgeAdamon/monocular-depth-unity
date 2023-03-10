using Unity.Barracuda;

namespace UnchartedLimbo.NN.Depth
{
    public static class BarracudaExtensions
    {
        /// <summary>
        /// Returns the maximum value in the given <see cref="BarracudaArray"/>.
        /// </summary>
        /// <param name="array">The  <see cref="BarracudaArray"/> to search for the maximum value.</param>
        /// <returns>The maximum value found in the  <see cref="BarracudaArray"/>. If the  <see cref="BarracudaArray"/> is empty, float.MinValue is returned.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the <see cref="BarracudaArray"/> parameter is null.</exception>
        public static float Max(this BarracudaArray array)
        {
            var max = float.MinValue;

            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] > max)
                    max = array[i];
            }

            return max;
        }

        /// <summary>
        /// Returns the minimum value in the given <see cref="BarracudaArray"/>.
        /// </summary>
        /// <param name="array">The  <see cref="BarracudaArray"/> to search for the minimum value.</param>
        /// <returns>The minimum value found in the  <see cref="BarracudaArray"/>. If the  <see cref="BarracudaArray"/> is empty, float.MaxValue is returned.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the <see cref="BarracudaArray"/> parameter is null.</exception>
        public static float Min(this BarracudaArray array)
        {
            var min = float.MaxValue;

            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] < min)
                    min = array[i];
            }

            return min;
        }
    }
}