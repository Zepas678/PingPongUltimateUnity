using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossMirage : BossController
{
    public float tiempoEntreEspejismos = 3.0f;
    public float alfaClones = 0.5f;
    public float spreadLateralClones = 1.5f;
    public float spreadVerticalClones = 0.75f;
    public float anguloAperturaClones = 25f;
    public float tiempoVidaClones = 2.0f;
    public int cantidadClones = 2;
    public GameObject prefabPelotaFalsa;
    public AudioClip sfxEspejismo;
    public float volumenSFX = 1.0f;
    [Range(0f, 1f)] public float volumenSFXEspejismo = 0.5f;
    [Header("Efectos")]
    public GameObject prefabEfectoHumo;
    [Header("Efectos de Desaparicion")]
    public AudioClip sfxDesaparicionClon;
    [Range(0f, 1f)] public float volumenSFXDesaparicion = 1.0f;
    public float duracionHumo = 1.5f;
    public bool clonesIgnoranGravedad = true;
    [Header("Dificultad CPU")]
    [Tooltip("Dificultad que se impone a la CPU durante el combate contra Mirage.")]
    public Dificultad dificultadCPU = Dificultad.Inhumano;
    private float ultimoEspejismoTiempo = -999f;
    private List<GameObject> clonesActivos = new List<GameObject>();
    private const string NOMBRE_CLON = "PelotaClon_Espejismo";
    public static BossMirage Instance { get; private set; }
    private void Awake()
    {
        Instance = this;
        ultimoEspejismoTiempo = -999f;
    }
    public override void IniciarCombate()
    {
        base.IniciarCombate();
        ultimoEspejismoTiempo = -999f;
        LimpiarClones();
        ForzarDificultadCPU();
    }
    public override void FinalizarCombate()
    {
        base.FinalizarCombate();
        LimpiarClones();
    }
    public void NotificarGolpeCPU()
    {
        if (!enabled || !combateActivo) return;
        ForzarDificultadCPU();
        Debug.Log("[BossMirage] NotificarGolpeCPU recibido.");
        ActivarHabilidad();
    }
    public override void ActivarHabilidad()
    {
        if (!combateActivo) return;
        if (Time.time < ultimoEspejismoTiempo + tiempoEntreEspejismos) return;
        GenerarEspejismo();
    }
    public override void DesactivarHabilidad()
    {
        LimpiarClones();
    }

    private void GenerarEspejismo()
    {
        PingPongBall bolaScript = FindObjectOfType<PingPongBall>();
        if (bolaScript == null) return;
        GameObject pelotaReal = bolaScript.gameObject;
        Rigidbody rbReal = pelotaReal.GetComponent<Rigidbody>();
        Vector3 velocidadPelota = rbReal != null ? rbReal.linearVelocity : Vector3.zero;
        if (sfxEspejismo != null)
            ReproducirSonido(sfxEspejismo, volumenSFXEspejismo);
        else
            Debug.LogWarning("[BossMirage] sfxEspejismo no asignado en el Inspector.");
        for (int i = 0; i < cantidadClones; i++)
        {
            GameObject baseClon = prefabPelotaFalsa != null ? prefabPelotaFalsa : pelotaReal;
            GameObject clon = Instantiate(baseClon, pelotaReal.transform.position, pelotaReal.transform.rotation);
            clon.name = NOMBRE_CLON;
            clon.tag = "Untagged";
            clon.transform.localScale = pelotaReal.transform.localScale;
            clon.transform.position = pelotaReal.transform.position;
            PingPongBall logicaClon = clon.GetComponent<PingPongBall>();
            if (logicaClon != null) Destroy(logicaClon);
            PingPongBall[] logicasHijas = clon.GetComponentsInChildren<PingPongBall>(true);
            foreach (PingPongBall l in logicasHijas) { if (l != null) Destroy(l); }
            Collider[] colliders = clon.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in colliders) { if (col != null) col.enabled = true; }
            Collider colliderClon = clon.GetComponentInChildren<Collider>(true);
            // Ignorar colision con la pelota real
            Collider colliderPelotaReal = pelotaReal.GetComponentInChildren<Collider>(true);
            if (colliderPelotaReal != null && colliderClon != null)
            {
                Physics.IgnoreCollision(colliderClon, colliderPelotaReal, true);
            }
            if (colliderClon != null) IgnorarColisionConRaquetas(colliderClon);
            Rigidbody rbClon = clon.GetComponent<Rigidbody>();
            if (rbClon != null)
            {
                rbClon.isKinematic = false;
                rbClon.useGravity = !clonesIgnoranGravedad;
                rbClon.Sleep();
                rbClon.WakeUp();
                float anguloClon = (i == 0) ? anguloAperturaClones : -anguloAperturaClones;
                Vector3 velocidadClon = Quaternion.Euler(0f, anguloClon, 0f) * velocidadPelota;
                velocidadClon.y += Random.Range(-spreadVerticalClones, spreadVerticalClones);
                rbClon.linearVelocity = velocidadClon;
            }
            AplicarTransparencia(clon);
            StartCoroutine(DestruirClon(clon, tiempoVidaClones));
            clonesActivos.Add(clon);
        }
        ultimoEspejismoTiempo = Time.time;
    }
    private void IgnorarColisionConRaquetas(Collider colliderClon)
    {
        if (colliderClon == null) return;
        GameObject[] todos = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject go in todos)
        {
            if (go == null) continue;
            string n = go.name.ToLowerInvariant();
            if (!n.Contains("raqueta") && !n.Contains("racket") && !n.Contains("pala")) continue;
            Collider[] colsR = go.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colsR)
            {
                if (c == null || c == colliderClon) continue;
                Physics.IgnoreCollision(colliderClon, c, true);
            }
        }
    }
    private void AplicarTransparencia(GameObject clon)
    {
        if (clon == null) return;
        Renderer[] renderers = clon.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;
                Color c;
                if (mat.HasProperty("_BaseColor")) c = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color")) c = mat.GetColor("_Color");
                else continue;
                c.a = alfaClones;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            }
        }
    }
    private IEnumerator DestruirClon(GameObject clon, float retraso)
    {
        if (clon == null) yield break;
        float transcurrido = 0f;
        Renderer rend = clon.GetComponentInChildren<Renderer>(true);
        Color colorOriginal = Color.white;
        string prop = "_Color";
        if (rend != null)
        {
            if (rend.material.HasProperty("_BaseColor")) { prop = "_BaseColor"; colorOriginal = rend.material.GetColor(prop); }
            else if (rend.material.HasProperty("_Color")) { prop = "_Color"; colorOriginal = rend.material.GetColor(prop); }
        }
        if (rend != null)
        {
            while (transcurrido < retraso)
            {
                if (clon == null) yield break;
                transcurrido += Time.deltaTime;
                float alpha = Mathf.Lerp(alfaClones, 0f, transcurrido / retraso);
                rend.material.SetColor(prop, new Color(colorOriginal.r, colorOriginal.g, colorOriginal.b, alpha));
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(retraso);
        }
        if (clon != null)
        {
            clonesActivos.Remove(clon);
            if (prefabEfectoHumo != null)
            {
                Quaternion rotacionCamara = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
                GameObject humo = Instantiate(prefabEfectoHumo, clon.transform.position, rotacionCamara);
                if (sfxDesaparicionClon != null)
                {
                    ReproducirSonido(sfxDesaparicionClon, volumenSFXDesaparicion);
                }
                else
                {
                    Debug.LogWarning("[BossMirage] sfxDesaparicionClon no asignado en el Inspector.");
                }
                StartCoroutine(DesvanecerHumo(humo, duracionHumo));
            }
            Destroy(clon);
        }
    }
    private IEnumerator DesvanecerHumo(GameObject humo, float duracion)
    {
        if (humo == null) yield break;
        float transcurrido = 0f;
        Renderer[] renderers = humo.GetComponentsInChildren<Renderer>(true);
        List<KeyValuePair<Material, Color>> matColors = new List<KeyValuePair<Material, Color>>();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            foreach (Material m in r.materials)
            {
                if (m == null) continue;
                string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : (m.HasProperty("_Color") ? "_Color" : "");
                if (!string.IsNullOrEmpty(prop))
                {
                    matColors.Add(new KeyValuePair<Material, Color>(m, m.GetColor(prop)));
                }
            }
        }
        while (transcurrido < duracion)
        {
            if (humo == null) yield break;
            transcurrido += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(transcurrido / duracion));
            foreach (var item in matColors)
            {
                if (item.Key == null) continue;
                string prop = item.Key.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                Color c = item.Value;
                c.a *= alpha;
                item.Key.SetColor(prop, c);
            }
            yield return null;
        }
        if (humo != null)
        {
            Destroy(humo);
        }
    }
    private void LimpiarClones()
    {
        for (int i = clonesActivos.Count - 1; i >= 0; i--)
        {
            if (clonesActivos[i] != null) Destroy(clonesActivos[i]);
        }
        clonesActivos.Clear();
    }
    private void ForzarDificultadCPU()
    {
        CPUControl cpu = FindObjectOfType<CPUControl>();
        if (cpu == null) return;
        if (cpu.dificultadActual == dificultadCPU) return;
        cpu.SetDificultad(dificultadCPU);
        Debug.Log("[BossMirage] Dificultad CPU forzada a " + dificultadCPU);
    }
    private void ReproducirSonido(AudioClip clip, float volumen)
    {
        if (clip == null)
        {
            Debug.LogWarning("[BossMirage] Intento de reproducir clip nulo. Asigna el AudioClip en el Inspector.");
            return;
        }
        AudioSource local = GetComponent<AudioSource>();
        if (local != null)
        {
            local.spatialBlend = 0f;
            local.PlayOneShot(clip, Mathf.Clamp01(volumen));
            return;
        }
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, Mathf.Clamp01(volumen));
    }
    private void OnDestroy()
    {
        LimpiarClones();
    }
}