using UnityEngine;
using System.Collections;

public class LaserSpawnController : MonoBehaviour
{
    [Header("References")]
    public Transform beam;
    public Transform[] brackets;
    public Transform impactDecal;
    public ParticleSystem fallParticles;
    public ParticleSystem impactFlash;
    public GameObject enemyPrefab;

    [Header("Settings")]
    public float warningDuration = 0.3f;
    public float chargeDuration = 0.6f;
    public float impactDuration = 0.4f;
    public AnimationCurve thicknessCurve = AnimationCurve.EaseInOut(0,0.05f,1,0.8f);

    Material beamMat;
    Vector3 beamOriginalScale;
    Vector3 beamOriginalPos;

    void Awake()
    {
        if(beam) {
            var rend = beam.GetComponent<Renderer>();
            if(rend) beamMat = rend.material;
            beamOriginalScale = beam.localScale;
            beamOriginalPos = beam.position;
            beam.gameObject.SetActive(false);
        }
        SetBracketsActive(false);
        if(impactDecal) impactDecal.gameObject.SetActive(false);
    }

    [ContextMenu("Spawn!")]
    public void Spawn() { StartCoroutine(SpawnSequence()); }

    IEnumerator SpawnSequence()
    {
        // Stage 1: thin warning line - image 1
        beam.gameObject.SetActive(true);
        beam.localScale = new Vector3(0.05f, beamOriginalScale.y, 0.05f);
        if(beamMat) beamMat.SetFloat("_BeamWidth", 0.05f);
        yield return new WaitForSeconds(warningDuration);

        // Stage 2: thicken + brackets - image 2
        SetBracketsActive(true);
        AnimateBracketsPop();
        float t = 0;
        if(fallParticles) fallParticles.Play();
        while(t < 1f) {
            t += Time.deltaTime / chargeDuration;
            float thick = thicknessCurve.Evaluate(t);
            if(beamMat) beamMat.SetFloat("_BeamWidth", Mathf.Lerp(0.05f, 0.8f, t));
            beam.localScale = new Vector3(thick, beamOriginalScale.y, thick);
            yield return null;
        }

        // Stage 3: impact column - image 3,4
        if(impactDecal) {
            impactDecal.gameObject.SetActive(true);
            impactDecal.localScale = Vector3.zero;
            StartCoroutine(ScaleTo(impactDecal, Vector3.one * 2f, 0.15f));
        }
        StartCoroutine(Shake(0.15f, 0.12f));
        yield return new WaitForSeconds(impactDuration);

        // Stage 4: spawn + retract - image 5
        if(beamMat) beamMat.SetFloat("_TopMelt", 1f);
        Vector3 startPos = beam.position;
        t = 0;
        while(t < 1f) {
            t += Time.deltaTime / 0.3f;
            beam.position = Vector3.Lerp(startPos, startPos + Vector3.up * 15f, t);
            if(beamMat) beamMat.SetFloat("_BeamWidth", Mathf.Lerp(0.8f, 0f, t));
            yield return null;
        }
        beam.gameObject.SetActive(false);
        beam.position = startPos;
        if(beamMat) beamMat.SetFloat("_TopMelt", 0.3f);

        if(impactFlash) impactFlash.Play();
        if(enemyPrefab) {
            var pos = impactDecal ? impactDecal.position : transform.position;
            var enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
            enemy.transform.localScale = Vector3.zero;
            StartCoroutine(ScaleTo(enemy.transform, Vector3.one, 0.25f));
        }

        SetBracketsActive(false);
        if(fallParticles) fallParticles.Stop();
    }

    void SetBracketsActive(bool active) {
        foreach(var b in brackets) if(b) b.gameObject.SetActive(active);
    }

    void AnimateBracketsPop() {
        foreach(var b in brackets) {
            if(!b) continue;
            b.localScale = Vector3.zero;
            StartCoroutine(ScaleTo(b, Vector3.one, 0.25f, Random.Range(0,0.1f)));
        }
    }

    IEnumerator ScaleTo(Transform tr, Vector3 to, float time, float delay=0f) {
        if(delay>0) yield return new WaitForSeconds(delay);
        Vector3 from = tr.localScale;
        float t=0;
        while(t<1f){ t+=Time.deltaTime/time; tr.localScale = Vector3.Lerp(from,to,t); yield return null; }
    }

    IEnumerator Shake(float duration, float magnitude) {
        if(!Camera.main) yield break;
        Vector3 original = Camera.main.transform.localPosition;
        float elapsed = 0;
        while(elapsed < duration) {
            Camera.main.transform.localPosition = original + Random.insideUnitSphere * magnitude;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Camera.main.transform.localPosition = original;
    }
}
