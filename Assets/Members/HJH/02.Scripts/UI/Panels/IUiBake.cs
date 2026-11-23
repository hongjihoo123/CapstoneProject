namespace Assets.Members.HJH._02.Scripts.UI
{
    // 레이아웃을 에디터에서 한 번 만들어(구워) 씬/프리팹에 저장하는 UI.
    // 실행 중에는 저장된 자식만 쓰고 새로 만들지 않음. 다시 만들려면 컴포넌트 ⋮ 메뉴 "레이아웃 굽기" 또는 HJH/UI 메뉴
    public interface IUiBake
    {
        void BakeLayout();
    }
}
