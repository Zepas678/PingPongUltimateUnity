using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// PEGAR ESTE SCRIPT EN EL GAMEOBJECT "BossCard" (el padre de BossName, BossSubtitle,
/// BossDescription y BossImage).
///
/// Qué hace:
/// - OnDisable: imprime el StackTrace de QUIÉN la apagó.
/// - Update: si BossCard o cualquiera de sus hijos (BossName, BossSubtitle,
///   BossDescription, BossImage) están desactivados (gameObject o componente),
///   los vuelve a activar inmediatamente.
/// </summary>
public class BossCardWatcher : MonoBehaviour
{
    TMPro.TMP_Text[] textos;
    UnityEngine.UI.Image[] imagenes;
    CanvasGroup[] canvasGroups;

    void Awake()
    {
        textos = GetComponentsInChildren<TMPro.TMP_Text>(true);
        imagenes = GetComponentsInChildren<UnityEngine.UI.Image>(true);
        canvasGroups = GetComponentsInChildren<CanvasGroup>(true);
        Debug.Log("[BossCardWatcher] BossCard DESPERTO frame=" + Time.frameCount);
    }

    void OnEnable()
    {
        Debug.Log("[BossCardWatcher] BossCard HABILITADA frame=" + Time.frameCount);
        // Mantener la tarjeta y todos sus hijos SIEMPRE visibles al abrir el menú.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (textos != null)
            foreach (var t in textos) if (t != null && !t.gameObject.activeSelf) t.gameObject.SetActive(true);
        if (imagenes != null)
            foreach (var img in imagenes) if (img != null && !img.gameObject.activeSelf) img.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        Debug.LogWarning("[BossCardWatcher] BossCard fue DESACTIVADA por: " + UnityEngine.StackTraceUtility.ExtractStackTrace());
    }

    void Update()
    {
        // 1) Si el propio GameObject BossCard está apagado, encenderlo.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Debug.LogWarning("[BossCardWatcher] BossCard reactivada (gameObject) en frame " + Time.frameCount);
        }

        // 2) Re-forzar TMP hijos que estén apagados (enabled=false).
        if (textos != null)
        {
            foreach (var t in textos)
            {
                if (t == null) continue;
                if (t.gameObject.activeInHierarchy && !t.enabled)
                {
                    t.enabled = true;
                    Debug.LogWarning("[BossCardWatcher] TMP '" + t.name + "' reactivado (enabled=false) frame " + Time.frameCount);
                }
                if (!t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(true);
                    Debug.LogWarning("[BossCardWatcher] TMP '" + t.name + "' GO reactivado frame " + Time.frameCount);
                }
            }
        }

        // 3) Re-forzar Image hijos apagados.
        if (imagenes != null)
        {
            foreach (var img in imagenes)
            {
                if (img == null) continue;
                if (img.gameObject.activeInHierarchy && !img.enabled)
                {
                    img.enabled = true;
                    Debug.LogWarning("[BossCardWatcher] Image '" + img.name + "' reactivada (enabled=false) frame " + Time.frameCount);
                }
                if (!img.gameObject.activeSelf)
                {
                    img.gameObject.SetActive(true);
                    Debug.LogWarning("[BossCardWatcher] Image '" + img.name + "' GO reactivado frame " + Time.frameCount);
                }
            }
        }

        // 4) Forzar CanvasGroup alpha = 1 si existe.
        if (canvasGroups != null)
        {
            foreach (var cg in canvasGroups)
            {
                if (cg != null && cg.alpha < 1f)
                {
                    cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                    Debug.LogWarning("[BossCardWatcher] CanvasGroup alpha restaurado a 1 frame " + Time.frameCount);
                }
            }
        }
    }
}