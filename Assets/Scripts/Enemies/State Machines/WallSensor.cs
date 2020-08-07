﻿using UnityEngine;

public class WallSensor : Sensor {

	public float distance = 1f;
	public bool useTrigger = false;
	public bool autoFlip = false;
	public bool nearWall;
	Vector2 size;
	int layerMask;

	new void Start() {
		base.Start();
		size = GetComponent<BoxCollider2D>().bounds.size * 0.75f;
		layerMask = 1 << LayerMask.NameToLayer(Layers.Ground);
	}

	void Update() {
		RaycastHit2D hit = Physics2D.BoxCast(
			this.transform.position,
			// don't touch floor/ceiling
			size * 0.8f,
			0,
			Vector2.right * e.ForwardScalar(),
			distance,
			layerMask
		);
		nearWall = hit.collider != null;
		if (nearWall) {
			if (useTrigger) animator.SetTrigger("NearWall");
			if (autoFlip) {
				Debug.Log("asdh");
				e.Flip();
			}
		}
		animator.SetBool("NearWall", nearWall);
	}
}
