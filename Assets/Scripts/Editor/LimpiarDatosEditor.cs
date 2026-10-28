using UnityEditor;
using UnityEngine;

public class LimpiarDatosEditor
{
    [MenuItem("Herramientas/Borrar PlayerPrefs (Reset Juego)")]
    public static void BorrarPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("¡PlayerPrefs limpiado por completo! El juego se ha reiniciado desde cero.");
    }
}
