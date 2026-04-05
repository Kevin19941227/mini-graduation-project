public interface ILobbyProvider
{
    void CreateLobby();
    void JoinLobby(string address);
    void LeaveLobby();

    string GetLobbyName();
    int GetMemberCount();
    int GetMaxMembers();
}