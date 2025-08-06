namespace GAToolAPI.Extensions;

public static class ParameterExtensions
{
    /// <summary>
    ///     Extension method to convert an anonymous object with nullable string properties into a dictionary with variable
    ///     names as keys
    /// </summary>
    /// <param name="parameters">Anonymous object containing the variables to convert</param>
    /// <returns>Dictionary with variable names as keys and non-null values</returns>
    public static Dictionary<string, string?> ToParameterDictionary(this object parameters)
    {
        var dictionary = new Dictionary<string, string?>();

        var properties = parameters.GetType().GetProperties();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(parameters) as string;
            if (!string.IsNullOrEmpty(value)) dictionary[prop.Name] = value;
        }

        return dictionary;
    }
}