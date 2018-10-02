using System.Collections;
using UnityEngine;

public class MakeMissAnimationScript : MonoBehaviour {

    private Material innerBoxMaterial;
    private Material outerBoxMaterial;
    private Color innerBoxColor;
    private Color outerBoxColor;

	void Awake () {
        innerBoxMaterial = transform.Find("basket_part_15").GetComponent<MeshRenderer>().material;
        outerBoxMaterial = transform.Find("basket_part_18").GetComponent<MeshRenderer>().material;
        innerBoxColor = innerBoxMaterial.color;
        outerBoxColor = outerBoxMaterial.color;
	}

    public void AnimateMake() {
        StartCoroutine(AnimateGreenColor());
    }

    public void AnimateMiss() {
        StartCoroutine(AnimateRedColor());
    }

    IEnumerator AnimateGreenColor() {
        innerBoxMaterial.color = Color.green;
        outerBoxMaterial.color = Color.green;

        yield return new WaitForSeconds(1.5f);

        innerBoxMaterial.color = innerBoxColor;
        outerBoxMaterial.color = outerBoxColor;
    }

    IEnumerator AnimateRedColor() {
        innerBoxMaterial.color = Color.red;
        outerBoxMaterial.color = Color.red;

        yield return new WaitForSeconds(1.5f);

        innerBoxMaterial.color = innerBoxColor;
        outerBoxMaterial.color = outerBoxColor;
    }
}
