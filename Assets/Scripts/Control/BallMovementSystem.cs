using OneBitLab.FluidSim;
using OneBitLab.Services;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(WaveSubdivideSystem))]
public class BallMovementSystem : ComponentSystem
{
    private float timeSinceLastWave = 0f;
    public float waveInterval = 0.3f; // ���Ʋ����ɵ�ʱ����

    //public float moveSpeed = 12.0f;
    public float moveSpeed = 20.0f;

    protected override void OnUpdate()
    {
        float deltaTime = UnityEngine.Time.deltaTime;

        timeSinceLastWave += deltaTime; // ����ʱ��

        Entities.WithAll<Tag_Player>().ForEach((ref Translation translation, ref Rotation rotation, ref Unity.Physics.PhysicsVelocity velocity) =>
        {
            // ��ȡ�û�������
            float verticalInput = Input.GetAxis("Vertical");
            float horizontalInput = Input.GetAxis("Horizontal");

            // �������뷽������ƶ���ǰ�������ˡ����ơ����ƣ�
            float3 moveDirection = new float3(horizontalInput, 0f, verticalInput);
            translation.Value += moveDirection * deltaTime * moveSpeed;

            if (math.lengthsq(moveDirection) > 0 && timeSinceLastWave >= waveInterval)
            {
                var messageQueue = MessageService.Instance.GetOrCreateMessageQueue<ParticleSpawnMessage>();
                messageQueue.Enqueue(new ParticleSpawnMessage { Pos = translation.Value });//���ɲ�����

                // ���ü�ʱ��
                timeSinceLastWave = 0f;
            }

            // �����ƶ���������ת����������÷���
            if (math.lengthsq(moveDirection) > 0)
            {
                //velocity.Angular += 
                quaternion targetRotation = quaternion.LookRotation(moveDirection, math.up());
                //rotation.Value += targetRotation * Quaternion.Inverse(rotation.Value);
                rotation.Value = math.slerp(rotation.Value, targetRotation, deltaTime * 5f); // ���� 5f �Ըı���ת�ٶ�
            }

        });
    }
}