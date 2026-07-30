# Adivinhe a Cor

Jogo multiplayer digital de adivinhação de cores, desenvolvido em Unity como projeto
acadêmico da Pós-Graduação em Desenvolvimento Full Stack.

Um jogador recebe uma cor secreta do tabuleiro e escreve uma dica de uma palavra. Os
outros tentam localizar a cor em uma grade de 480 tonalidades, e os pontos são calculados
pela proximidade entre o palpite e a cor-alvo. Vence quem chegar primeiro a 25 pontos.

## Tecnologias

- **Unity 6 LTS** (6000.0.80f1), C#
- **Netcode for GameObjects** — lógica de rede host-autoritativa
- **Multiplayer Services SDK + Relay** — sessões online por código de sala (WebGL via WSS)
- **TextMeshPro**, uGUI — interface
- Arquitetura em camadas (`ColorGuesser.Core` / `.Game` / `.Net`) documentada com o
  **C4 Model**; regras de jogo isoladas em código puro e cobertas por testes unitários

## Como jogar

1. Escolha um apelido e uma cor.
2. **Criar sala** para hospedar (você recebe um código) ou **Entrar** com o código de um amigo.
3. Todos marcam "Pronto"; o anfitrião inicia a partida.
4. A cada rodada, um jogador é o **Mestre da Cor**: ele vê a cor secreta e dá duas dicas,
   de uma palavra cada. Os demais fazem um palpite após cada dica.
5. A cor é revelada, os pontos são somados e o Mestre da Cor passa para o próximo jogador.

## Estrutura

```
Assets/Scripts/Core    regras do jogo (sem Unity UI, sem rede) + testes
Assets/Scripts/Game    interface e visualização do tabuleiro
Assets/Scripts/Net     sessões, sincronização e host autoritativo
Assets/Scripts/Editor  geradores de prefabs e utilitários de projeto
Assets/Resources       dados do tabuleiro (BoardData.csv)
```

## Aviso / Disclaimer

Este é um **projeto acadêmico independente**, sem finalidade comercial e **sem qualquer
vínculo, patrocínio ou afiliação** com The OP Games ou com qualquer editora de jogos de
tabuleiro. A mecânica de associação entre cores e palavras foi usada apenas como
**referência de estudo** — mecânicas e regras de jogo não são protegidas por direito
autoral. Nomes, identidade visual e demais componentes deste projeto são próprios.

*This is an independent, non-commercial academic project. It is not affiliated with,
sponsored by, or endorsed by The OP Games or any board game publisher.*

## Licença

Código disponibilizado para fins educacionais. Consulte o autor antes de reutilizar.
