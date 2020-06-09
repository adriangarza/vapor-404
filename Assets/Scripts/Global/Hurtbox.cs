﻿using UnityEngine;

public class Hurtbox : MonoBehaviour {

	public GameObject parentObject;
	
	[Header("For Targeting Systems")]
	public bool overrideTargetPosition;

	void Start() {
		if (parentObject == null && GetComponentInParent<Entity>() != null) {
			parentObject = GetComponentInParent<Entity>().gameObject;
		}
	}

	public Entity GetParent() {
		if (parentObject == null) return null;
		return parentObject.GetComponent<Entity>();
	}

	virtual public bool OnHit(Attack a) {
		if (parentObject != null) {
			parentObject.GetComponent<Entity>().OnHit(a);
		}
		if (a.hitmarker != null) a.MakeHitmarker(this.transform);
		return true;
	}
}
