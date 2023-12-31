//! Instruction: InitializeThread
use crate::{ID, THREAD_AUTHORITY_SEED};
use anchor_lang::prelude::*;
use anchor_lang::solana_program::{
    instruction::Instruction, native_token::LAMPORTS_PER_SOL, system_program,
};
use anchor_lang::InstructionData;
use clockwork_sdk::state::Thread;

use super::push_in_direction::Highscore;
use super::Pricepool;

pub fn start_thread(ctx: Context<StartThread>, thread_id: Vec<u8>) -> Result<()> {
    let system_program = &ctx.accounts.system_program;
    let clockwork_program = &ctx.accounts.clockwork_program;
    let payer = &ctx.accounts.payer;
    let thread = &ctx.accounts.thread;
    let thread_authority = &ctx.accounts.thread_authority;
    let highscore = &mut ctx.accounts.highscore;

    // 1️⃣ Prepare an instruction to automate.
    //    In this case, we will automate the ThreadTick instruction.
    let target_ix = Instruction {
        program_id: ID,
        accounts: crate::__client_accounts_thread_tick::ThreadTick {
            highscore: highscore.key(),
            price_pool: ctx.accounts.price_pool.key(),
            thread: thread.key(),
            thread_authority: thread_authority.key(),
            system_program: system_program.key(),
        }
        .to_account_metas(Some(true)),
        data: crate::instruction::ResetAndDistribute {}.data(),
    };

    // 2️⃣ Define a trigger for the thread.
    let trigger = clockwork_sdk::state::Trigger::Cron {
        schedule: "0 0 1 * * SUN".into(),
        skippable: true,
    };
    // For testing run it every minute
    //let trigger = clockwork_sdk::state::Trigger::Cron {
    //    schedule: "*/60 * * * * *".into(),
    //    skippable: true,
    //};
    
    // 3️⃣ Create a Thread via CPI
    let bump = *ctx.bumps.get("thread_authority").unwrap();
    clockwork_sdk::cpi::thread_create(
        CpiContext::new_with_signer(
            clockwork_program.to_account_info(),
            clockwork_sdk::cpi::ThreadCreate {
                payer: payer.to_account_info(),
                system_program: system_program.to_account_info(),
                thread: thread.to_account_info(),
                authority: thread_authority.to_account_info(),
            },
            &[&[THREAD_AUTHORITY_SEED, &[bump]]],
        ),
        LAMPORTS_PER_SOL / 2, // amount of sol for the thread which pays the transaction fees
        thread_id,            // id
        vec![target_ix.into()], // instructions
        trigger,              // trigger
    )?;

    Ok(())
}

#[derive(Accounts)]
#[instruction(thread_id: Vec<u8>)]
pub struct StartThread<'info> {
    #[account( 
        seeds = [b"highscore_list_v2".as_ref()],
        bump,
    )]
    pub highscore: Account<'info, Highscore>,
    #[account( 
        seeds = [b"price_pool".as_ref()],
        bump,
    )]
    pub price_pool: Account<'info, Pricepool>,
    /// The Clockwork thread program.
    #[account(address = clockwork_sdk::ID)]
    pub clockwork_program: Program<'info, clockwork_sdk::ThreadProgram>,

    /// The signer who will pay to initialize the program.
    /// (not to be confused with the thread executions).
    #[account(mut)]
    pub payer: Signer<'info>,

    #[account(address = system_program::ID)]
    pub system_program: Program<'info, System>,

    /// Address to assign to the newly created thread.
    #[account(mut, address = Thread::pubkey(thread_authority.key(), thread_id))]
    pub thread: SystemAccount<'info>,

    /// The pda that will own and manage the thread.
    #[account(seeds = [THREAD_AUTHORITY_SEED], bump)]
    pub thread_authority: SystemAccount<'info>,
}

#[derive(Accounts)]
pub struct ThreadTick<'info> {
    #[account(mut)]
    pub highscore: Account<'info, Highscore>,
    #[account(mut)]
    pub price_pool: Account<'info, Pricepool>,

    /// Verify that only this thread can execute the ThreadTick Instruction
    #[account(signer, constraint = thread.authority.eq(&thread_authority.key()))]
    pub thread: Account<'info, Thread>,

    /// The Thread Admin
    /// The authority that was used as a seed to derive the thread address
    /// `thread_authority` should equal `thread.thread_authority`
    #[account(seeds = [THREAD_AUTHORITY_SEED], bump)]
    pub thread_authority: SystemAccount<'info>,
    pub system_program: Program<'info, System>,
}
