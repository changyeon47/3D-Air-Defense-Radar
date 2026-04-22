/*
 * LiDARReceiver.cs
 * ROS-TCP-Connector로 /detected_targets (PoseArray) 를 구독한다.
 * lidar_processor_node 가 클러스터링한 결과를 받아 LiDARTargetManager 에 넘긴다.
 *
 * 사용법:
 *   - Robotics > ROS Settings 에서 ROS IP / Port(10000) 설정
 *   - WSL: ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=0.0.0.0
 *   - WSL: ros2 launch air_defense_sim sim.launch.py
 */

using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

[Serializable]
public class LiDARTarget
{
    public int   id;
    public float x;
    public float y;
    public float z;
}

public class LiDARReceiver : MonoBehaviour
{
    [Header("ROS 토픽")]
    public string topicName = "/detected_targets";

    [Header("연결 대상")]
    public LiDARTargetManager targetManager;

    void Start()
    {
        if (targetManager == null)
            targetManager = FindFirstObjectByType<LiDARTargetManager>();

        ROSConnection.GetOrCreateInstance().Subscribe<PoseArrayMsg>(
            topicName, OnTargetsReceived);

        Debug.Log($"[LiDAR] ROS 구독 시작: {topicName}");
    }

    void OnTargetsReceived(PoseArrayMsg msg)
    {
        LiDARTarget[] targets = new LiDARTarget[msg.poses.Length];
        for (int i = 0; i < msg.poses.Length; i++)
        {
            var pos = msg.poses[i].position;
            targets[i] = new LiDARTarget
            {
                id = i,
                x  = (float)pos.x,
                y  = (float)pos.y,
                z  = (float)pos.z,
            };
        }

        if (msg.poses.Length > 0)
            Debug.Log($"[LiDAR] 타겟 수신: {msg.poses.Length}개");

        targetManager?.UpdateTargets(targets);
    }
}
