use anchor_lang::error_code;

#[error_code]
pub enum Solana2048Error {
    #[msg("Wrong Authority")]
    WrongAuthority,
    #[msg("Game not over yet")]
    GameNotOverYet,
}
