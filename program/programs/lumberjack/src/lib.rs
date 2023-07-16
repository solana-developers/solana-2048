use anchor_lang::prelude::*;
use gpl_session::{SessionError, SessionToken, session_auth_or, Session};
pub mod state;
pub use state::*;

declare_id!("6oKZFmFvcb69ThDuZjrsHABn4A6GMUpPGWNhxJKazWVB");

#[error_code]
pub enum GameErrorCode {
    #[msg("Wrong Authority")]
    WrongAuthority,
    #[msg("Game not over yet")]
    GameNotOverYet,
}

#[program]
pub mod lumberjack {
    use super::*;

    pub fn init_player(mut ctx: Context<InitPlayer>) -> Result<()> {
        let account = &mut ctx.accounts;
        msg!("init");
        account.player.board = account.player.board.Init();
        ctx.accounts.player.authority = ctx.accounts.signer.key();
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn push_in_direction(mut ctx: Context<PushInDirection>, direction: u8, counter: u8) -> Result<()> {
        let account = &mut ctx.accounts;
        let result = account.player.board.push(direction);
        // let was_game_over = account.player.game_over;
        account.player.score += result.0;
        //if !was_game_over && result.2 { // Lets just always save the highscore 
            msg!("Game over save highscore");
            let mut found = false;
            for n in 0..account.highscore.data.len() {
                if account.highscore.data[n].player == *account.player.to_account_info().key {
                    if account.highscore.data[n].score < account.player.score {
                        account.highscore.data[n].score = account.player.score;
                    }
                    found = true;
                }
            }
            
            if !found {
                account.highscore.data.push(HighscoreEntry {
                    score: account.player.score,
                    player: *account.player.to_account_info().key,
                    nft: *account.avatar.to_account_info().key,
                });
            
                account.highscore.data.sort_unstable_by(|a, b| b.score.cmp(&a.score));
                account.highscore.data.truncate(10);
            }
        //}
        //account.player.moved = result.1; // Probably not needed
        account.player.game_over = result.2;
        account.player.direction = direction;
        account.player.new_tile_x = result.3;
        account.player.new_tile_y = result.4;
        account.player.new_tile_level = result.5;
        msg!("Yo move tile moved:{} gamover:{} x:{} y:{} level:{}", result.1, result.2, result.3, result.4, result.5);
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn restart(mut ctx: Context<PushInDirection>, direction: u8, counter: u8) -> Result<()> {
        let account = &mut ctx.accounts;
        // For testing allow always resetting the game
        //if (account.player.game_over) {
        account.player.board = account.player.board.Init();
        account.player.score = 0;
        account.player.game_over = false;
        account.player.new_tile_level = 0;
        //} else {
        //    return err!(GameErrorCode::NotEnoughEnergy);
        //}
        msg!("Game reseted");
        Ok(())
    }

}

#[derive(Accounts)]
pub struct InitPlayer <'info> {
    #[account( 
        init,
        payer = signer,
        space = 1000,
        seeds = [b"player7".as_ref(), signer.key().as_ref(), avatar.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account( 
        init_if_needed,
        payer = signer,
        space = 1000,
        seeds = [b"highscore_list".as_ref()],
        bump,
    )]
    pub highscore: Account<'info, Highscore>,
    #[account(mut)]
    pub signer: Signer<'info>,
    /// CHECK: Unchecked until I can get SPL and Meta data to work
    pub avatar: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

#[account]
pub struct PlayerData {
    pub authority: Pubkey,
    pub board: BoardData,
    pub score: u32,
    pub game_over: bool,
    pub direction: u8,
    pub top_tile: u32,
    pub new_tile_x: u8, 
    pub new_tile_y: u8,
    pub new_tile_level: u32,
}

#[account]
pub struct Highscore {
    pub data: Vec<HighscoreEntry>,
}

#[derive(Default, AnchorSerialize, AnchorDeserialize, Clone, Copy, Debug)]
pub struct HighscoreEntry {
    pub score: u32,
    pub player: Pubkey,
    pub nft: Pubkey,
}

#[derive(Accounts, Session)]
pub struct PushInDirection <'info> {
    #[session(
        // The ephemeral key pair signing the transaction
        signer = signer,
        // The authority of the user account which must have created the session
        authority = player.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,

    #[account( 
        mut,
        seeds = [b"player7".as_ref(), player.authority.key().as_ref(), avatar.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account( 
        mut,
        seeds = [b"highscore_list".as_ref()],
        bump,
    )]
    pub highscore: Account<'info, Highscore>,
    #[account(mut)]
    pub signer: Signer<'info>,
    /// CHECK: Unchecked until I can get SPL and Meta data to work
    pub avatar: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}