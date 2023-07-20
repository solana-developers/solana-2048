import * as anchor from "@project-serum/anchor";
//import { SolanaTwentyfourtyeight } from "../target/types/solana_twentyfourtyeight";
import { publicKey } from "@project-serum/anchor/dist/cjs/utils";
import { ClockworkProvider } from "@clockwork-xyz/sdk";
import { IDL, SolanaTwentyfourtyeight } from "../target/types/solana_twentyfourtyeight"
import { clusterApiUrl, Connection, Keypair, PublicKey } from "@solana/web3.js"
import {
  Program,
  AnchorProvider,
  Idl,
  setProvider,
} from "@project-serum/anchor"

const threadId = "weekly_highscore_v2";

describe("solana-2048", () => {
  const provider = anchor.AnchorProvider.env();
  anchor.setProvider(provider);
  const clockworkProvider = ClockworkProvider.fromAnchorProvider(provider);
  const programId = new PublicKey("BTN22dEcBJcDF1vi81x5t3pXtD49GFA4cn3vDDrEyT3r")

  const program = new Program(
    IDL as Idl,
    programId
  ) as unknown as Program<SolanaTwentyfourtyeight>
  
  //const program = anchor.workspace.SolanaTwentyfourtyeight as Program<SolanaTwentyfourtyeight>;
  console.log("program", program);
  it("Start Thread!", async () => {
    console.log("Starting thread");
    await ResetThread();
    await StartThread();    
  });

  async function StartThread() {

    const [highscore] = anchor.web3.PublicKey.findProgramAddressSync(
      [Buffer.from("highscore_list_v2")],
      program.programId
    );

    const [price_pool] = anchor.web3.PublicKey.findProgramAddressSync(
      [Buffer.from("price_pool")],
      program.programId
    );

    const [threadAuthority] = publicKey.findProgramAddressSync(
        [anchor.utils.bytes.utf8.encode("authority")], // 👈 make sure it matches on the prog side
        program.programId
    );
    const [threadAddress, threadBump] = clockworkProvider.getThreadPDA(threadAuthority, threadId)

    const tx = await program.methods.startThread(Buffer.from(threadId))
    .accounts({
      highscore: highscore,
      pricePool: price_pool,
      thread: threadAddress,
      threadAuthority: threadAuthority,
      clockworkProgram: clockworkProvider.threadProgram.programId,
      systemProgram: anchor.web3.SystemProgram.programId,
    }).rpc();
    
    console.log("Create thread instruction", tx);
  };

  async function ResetThread() {

    const [threadAuthority] = publicKey.findProgramAddressSync(
        [anchor.utils.bytes.utf8.encode("authority")], // 👈 make sure it matches on the prog side
        program.programId
    );
    const [threadAddress, threadBump] = clockworkProvider.getThreadPDA(threadAuthority, threadId)

    const tx = await program.methods.resetThread(Buffer.from(threadId))
    .accounts({
      payer: provider.wallet.publicKey,
      thread: threadAddress,
      threadAuthority: threadAuthority,
      clockworkProgram: clockworkProvider.threadProgram.programId,
    }).rpc({ skipPreflight: true });
    
    console.log("Create thread instruction", tx);
  };
});
