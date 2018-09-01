//
// @brief: 预制体管理类
// @version: 1.0.0
// @author helin
// @date: 03/7/2018
// 
// 
//

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class prefab {
    // Use this for initialization
    void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    public static GameObject create(string path, UnityObject obj, GameObject parentObj = null)
    {
#if _CLIENTLOGIC_
        GameObject gameObject = create(path);
        obj.m_gameObject = gameObject;

        if (null != parentObj)
        {
            obj.m_gameObject.transform.SetParent(parentObj.gameObject.transform);
        }

        return gameObject;
#else
        return null;
#endif
    }

    private static GameObject create(string path)
    {
        GameObject obj = (GameObject)GameObject.Instantiate(Resources.Load(path));

        return obj;
    }
}
