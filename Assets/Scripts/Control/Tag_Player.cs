using Unity.Entities;
using UnityEngine;

[GenerateAuthoringComponent]//��Ӵ�ע��ʹ�����ܹ���������Entity��  https://www.cnblogs.com/OtusScops/p/16885815.html
public struct Tag_Player : IComponentData
{

}
public struct BuoyancyComponent : IComponentData
{
    //����ֻ�Ƕ��壬��û�б��Զ����أ���Ҫ�ֶ�����
    public float buoyancyFactor; // ����ϵ��
}