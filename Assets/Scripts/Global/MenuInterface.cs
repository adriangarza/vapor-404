﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuInterface : MonoBehaviour {

	public void LoadScene(string sceneName) {
		GlobalController.LoadScene(sceneName);
	}
}
