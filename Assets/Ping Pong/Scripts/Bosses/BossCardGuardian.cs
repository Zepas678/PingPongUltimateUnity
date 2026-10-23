using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossCardGuardian : MonoBehaviour
{
    void Update()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        Transform[] todos = GetComponentsInChildren<Transform>(true);
        if (todos != null)
        {
            foreach (Transform t in todos)
            {
                if (t != null && !t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);
            }
        }

        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
        if (textos != null)
            foreach (var t in textos) if (t != null) t.enabled = true;

        Image[] imagenes = GetComponentsInChildren<Image>(true);
        if (imagenes != null)
            foreach (var img in imagenes) if (img != null) img.enabled = true;
    }

    void LateUpdate()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        foreach (Transform t in transform)
        {
            if (t != null && !t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }

        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
        if (textos != null)
            foreach (var t in textos) if (t != null) t.enabled = true;

        Image[] imagenes = GetComponentsInChildren<Image>(true);
        if (imagenes != null)
            foreach (var img in imagenes) if (img != null) img.enabled = true;

        Canvas.ForceUpdateCanvases();
    }
}
