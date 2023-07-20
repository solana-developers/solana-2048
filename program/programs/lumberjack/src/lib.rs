pub mod state;
use anchor_lang::prelude::*;
use anchor_lang::solana_program::native_token::LAMPORTS_PER_SOL;
pub use state::*;
pub mod errors;
use anchor_lang::InstructionData;
use instructions::*;
pub mod instructions;

declare_id!("BTN22dEcBJcDF1vi81x5t3pXtD49GFA4cn3vDDrEyT3r");

#[error_code]
pub enum GameErrorCode {
    #[msg("Wrong Authority")]
    WrongAuthority,
    #[msg("Game not over yet")]
    GameNotOverYet,
}

/// Seed for thread_authority PDA.
pub const THREAD_AUTHORITY_SEED: &[u8] = b"authority";

// Total fee to start a game will be 0.001 sol
// The game dev wallet can be configured in the client.
// So everyone running a client can earn some sol.
pub const JACKPOT_ENTRY: u64 = (LAMPORTS_PER_SOL / 10000) * 6; // 0.0006 SOL
pub const GAME_DEV_FEE: u64 = (LAMPORTS_PER_SOL / 10000) * 4; // 0.0004 SOL

#[program]
pub mod solana_twentyfourtyeight {

    use clockwork_sdk::state::ThreadResponse;
    use solana_program::instruction::Instruction;

    use super::*;

    pub fn init_player(ctx: Context<InitPlayer>) -> Result<()> {
        instructions::init_player(ctx)
    }

    pub fn reset_weekly_highscore(
        ctx: Context<ResetWeeklyHighscore>,
        thread_id: Vec<u8>,
    ) -> Result<()> {
        msg!("Reset weekly highscore called");
        instructions::reset_weekly_highscore(ctx)
    }

    pub fn reset_and_distribute(ctx: Context<ThreadTick>) -> Result<ThreadResponse> {
        msg!("Reset and distribute called");
        let mut place_1_option: Option<Pubkey> = None;
        if !ctx.accounts.highscore.weekly.is_empty() {
            place_1_option = Some(ctx.accounts.highscore.weekly[0].player);
        }
        let mut place_2_option: Option<Pubkey> = None;
        if ctx.accounts.highscore.weekly.len() > 1 {
            place_2_option = Some(ctx.accounts.highscore.weekly[1].player);
        }
        let mut place_3_option: Option<Pubkey> = None;
        if ctx.accounts.highscore.weekly.len() > 2 {
            place_3_option = Some(ctx.accounts.highscore.weekly[2].player);
        }

        let target_ix = Instruction {
            program_id: ID,
            accounts: crate::accounts::ResetWeeklyHighscore {
                highscore: ctx.accounts.highscore.key(),
                place_1: place_1_option,
                place_2: place_2_option,
                place_3: place_3_option,
                price_pool: ctx.accounts.price_pool.key(),
                system_program: ctx.accounts.system_program.key(),
                thread: ctx.accounts.thread.key(),
                thread_authority: ctx.accounts.thread_authority.key(),
            }
            .to_account_metas(Some(true)),
            data: crate::instruction::ResetWeeklyHighscore {
                thread_id: "weekly_highscore_v2".as_bytes().to_vec(),
            }
            .data(),
        }
        .into();

        // 2️⃣ Define a trigger for the thread.
        let trigger = clockwork_sdk::state::Trigger::Now {};

        //let trigger = clockwork_sdk::state::Trigger::Cron {
        //    schedule: format!("*/{} * * * * * *", 60).into(),
        //    skippable: true,
        //};

        Ok(ThreadResponse {
            close_to: None,
            dynamic_instruction: Some(target_ix),
            trigger: None,
        })
    }

    pub fn push_in_direction(
        ctx: Context<PushInDirection>,
        direction: u8,
        counter: u8,
    ) -> Result<()> {
        instructions::push_in_direction(ctx, direction, counter)
    }

    pub fn restart(ctx: Context<Restart>) -> Result<()> {
        instructions::restart(ctx)
    }

    pub fn start_thread(ctx: Context<StartThread>, thread_id: Vec<u8>) -> Result<()> {
        instructions::start_thread(ctx, thread_id)
    }

    pub fn pause_thread(ctx: Context<PauseThread>, thread_id: Vec<u8>) -> Result<()> {
        instructions::pause_thread(ctx, thread_id)
    }

    pub fn resume_thread(ctx: Context<ResumeThread>, thread_id: Vec<u8>) -> Result<()> {
        instructions::resume_thread(ctx, thread_id)
    }

    pub fn reset_thread(ctx: Context<ResetThread>, thread_id: Vec<u8>) -> Result<()> {
        instructions::reset_thread(ctx, thread_id)
    }
}
