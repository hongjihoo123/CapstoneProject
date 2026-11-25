namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 소리가 나가는 믹서 그룹. 이름이 믹서(Resources/HJH/MainMixer)의 그룹 이름과 같아야 함 (Master/<이름>)
    // 음량 조절용으로 믹서에 "Vol_<이름>" 파라미터를 노출해 둠 (Vol_Master 포함)
    public enum SoundBus
    {
        Sfx,        // 플레이어·무기·스킬·피격
        Enemy,      // 적 공격·목소리·발소리
        Ui,         // 버튼·알림 (2D)
        Ambience,   // 폭포·물·바람 같은 환경음
        Music       // BGM
    }

    // 소리 통이 가득 찼을 때 무엇을 먼저 끊을지. 낮은 것부터 끊고, 같으면 오래된 것부터.
    // 가득 찬 통이 전부 같거나 더 높은 우선순위면 새 요청이 버려짐.
    //  Critical  플레이어 피격, 보스 패턴 경고
    //  High      처치, 스킬 시전·폭발, 적 공격
    //  Normal    일반 피격, 발사, 대시
    //  Low       적 발소리, 환경음, 반복되는 잡음
    public enum SoundPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public enum SoundSpace
    {
        World3D,   // 소리가 난 위치에서 들림 (거리·방향)
        Flat2D     // 위치 없음 (UI·음악)
    }
}
