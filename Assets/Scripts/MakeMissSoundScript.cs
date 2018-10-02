using UnityEngine;

public class MakeMissSoundScript : MonoBehaviour {

    private AudioSource makeAudioSource;
    private AudioSource missAudioSource;

	void Start () {
        makeAudioSource = transform.Find("MakeAudioObject").GetComponent<AudioSource>();
        missAudioSource = transform.Find("MissAudioObject").GetComponent<AudioSource>();
    }
	
    public void PlayMakeAudio() {
        makeAudioSource.Play();
    }

    public void PlayMissAudio() {
        missAudioSource.Play();
    }
}
