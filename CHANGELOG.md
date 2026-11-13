## v0.1.0 (2026-11-12)

### Feat

- **conexoes**: adiciona diálogo de nova conexão
- **ui**: adiciona ícone próprio do app
- **ui**: redesenha telas com modo leitura e salvamento automático
- **ui**: adiciona estilos comuns e componentes de linha
- **dashboard**: adiciona tela inicial com dashboard e menu de navegação
- **status**: adiciona teste de porta TCP dos servidores
- **conexoes**: adiciona favoritos e contador de uso
- **ui**: adiciona janela principal de gerenciamento de conexões
- **conexoes**: abre sessões RDP via mstsc e SSH via terminal
- **credenciais**: guarda senhas no Gerenciador de Credenciais do Windows
- **conexoes**: adiciona modelo e persistência das conexões

### Refactor

- **conexoes**: extrai interpretação do endereço para ConnectionAddress
- **ssh**: expõe detecção do OpenSSH e do Windows Terminal
