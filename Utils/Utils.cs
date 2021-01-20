namespace MyHome.Utils
{
    public static class Utils
    {
        /// <summary>
        /// Sets a non-automatic property backing field and invokes property changed event.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="backingField">Reference to the backing field.</param>
        /// <param name="value">Value to be set to the backing field.</param>
        /// <returns>Returns true if the backing field value is successfully updated.</returns>
        public static bool SetPropertyBackingField<T>(ref T backingField, T value)
        {
            if (!typeof(T).IsValueType)
            {
                if (ReferenceEquals(backingField, value))
                {
                    return false;
                }
            }
            else
            {
                if (backingField.Equals(value))
                {
                    return false;
                }
            }

            backingField = value;
            return true;
        }
    }
}
