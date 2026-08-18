using System.Diagnostics;
using System.Globalization;
using Test;
using Test.SingleStepTests;
using Util.Extensions;
using CPU = Silly6502.MOS6502;

var files = Directory.GetFiles("Tests/SingleStepTests/6502/v1", "*.json");
Array.Sort(files);

var success = 0;
var failed = 0;

var sw = new Stopwatch();
var cycles = 0UL;

foreach (var filePath in files)
{
	var num = byte.Parse(Path.GetFileNameWithoutExtension(filePath), NumberStyles.AllowHexSpecifier);

	Console.Write($"Running 0x{num:X2} tests...");

	using var fs = File.OpenRead(filePath);
	var tests = TestDataTest.Deserialize(fs) ?? throw new("Failed to read tests.");

	var suc = true;


	foreach (var test in tests)
	{
		try
		{
			RunTest(test);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"failed ({test.Name}): {ex.Message}");
			suc = false;
			break;
		}
	}

	if (suc)
	{
		Console.WriteLine("success!");
		success++;
	}
	else
	{
		failed++;
	}
}

var elapsedMillis = sw.Elapsed.TotalMilliseconds;

Console.WriteLine($"Time: {elapsedMillis:F2}ms");
Console.WriteLine($"Cycles: {cycles}");
Console.WriteLine($"Speed: {cycles * 1000.0 / elapsedMillis:N0} cycles/sec");

Console.WriteLine($"Success: {success}");
Console.WriteLine($"Failed: {failed}");

return;

void RunTest(TestDataTest test)
{
	//Console.WriteLine(test.Name);

	// Create test memory bus and CPU
	var bus = new TestBus();
	var cpu = new CPU(bus);

	// Every time an instruction finishes executing, the done flag is set to false
	var done = false;
	cpu.InstructionFinished += () => done = true;

	// Set reset vector to target PC and reset the CPU
	bus.Ram[CPU.VectorReset] = (byte)test.InitialState.RegPC.GetBits(0, 0xFF);
	bus.Ram[CPU.VectorReset + 1] = (byte)test.InitialState.RegPC.GetBits(8, 0xFF);
	cpu.Reset();

	// Keep ticking the CPU until the reset has finished
	done = false;
	while (!done)
		cpu.Tick();

	// Set up the initial CPU state
	cpu.RegA = test.InitialState.RegA;
	cpu.RegX = test.InitialState.RegX;
	cpu.RegY = test.InitialState.RegY;
	cpu.RegPC = test.InitialState.RegPC;
	cpu.RegSPLow = test.InitialState.RegSPLow;
	cpu.RegStatus = test.InitialState.RegStatus;

	// ...and memory
	foreach (var ram in test.InitialState.Ram)
		bus.Ram[ram.Address] = ram.Value;

	sw.Start();
	
	foreach (var cycle in test.Cycles)
	{
		done = false;

		//Console.WriteLine($"{(cpu.BusWrite ? "Write" : "Read")} [0x{cpu.BusAddress:X4}] = 0x{cpu.BusData:X2}");
		cpu.Tick();
		cycles++;

		if (cycle.Operation == TestDataBusOperation.Read != cpu.BusRead)
			throw new($"Bus operation was {(cpu.BusRead ? "read" : "write")}, should have been {(cycle.Operation == TestDataBusOperation.Read ? "read" : "write")}");

		if (cpu.BusAddress != cycle.Address)
			throw new($"Bus address was 0x{cpu.BusAddress:X4}, should have been 0x{cycle.Address:X4}");

		if (cpu.BusData != cycle.Data)
			throw new($"Bus data was 0x{cpu.BusData:X2}, should have been 0x{cycle.Data:X2}");
	}

	sw.Stop();
	/*
	if (!done)
		throw new("Expected end of instruction on last test cycle.");
	*/

	if (cpu.RegA != test.FinalState.RegA)
		throw new NotImplementedException($"A was 0x{cpu.RegA:X2}, should have been 0x{test.FinalState.RegA:X2}");

	if (cpu.RegX != test.FinalState.RegX)
		throw new NotImplementedException($"X was 0x{cpu.RegX:X2}, should have been 0x{test.FinalState.RegX:X2}");

	if (cpu.RegY != test.FinalState.RegY)
		throw new NotImplementedException($"Y was 0x{cpu.RegY:X2}, should have been 0x{test.FinalState.RegY:X2}");

	if (cpu.RegPC != test.FinalState.RegPC)
		throw new NotImplementedException($"PC was 0x{cpu.RegPC:X4}, should have been 0x{test.FinalState.RegPC:X4}");

	if (cpu.RegSPLow != test.FinalState.RegSPLow)
		throw new NotImplementedException($"SPLow was 0x{cpu.RegSPLow:X2}, should have been 0x{test.FinalState.RegSPLow:X2}");

	if (cpu.RegStatus != test.FinalState.RegStatus)
		throw new NotImplementedException($"Status was 0x{cpu.RegStatus:X2}, should have been 0x{test.FinalState.RegStatus:X2}");


	foreach (var ram in test.FinalState.Ram)
	{
		if (bus.Ram[ram.Address] != ram.Value)
			throw new($"RAM [0x{ram.Address:X4}] was 0x{bus.Ram[ram.Address]:X2}, should have been 0x{ram.Value:X2}");
	}
}

static void PrintTest(TestDataTest test)
{
	Console.WriteLine($"Test: {test.Name}");

	Console.WriteLine("Cycles:");
	foreach (var cycle in test.Cycles)
		Console.WriteLine($"  {cycle.Operation} [0x{cycle.Address:X4}] = 0x{cycle.Data:X2}");

	Console.WriteLine("Initial State:");
	PrintState(test.InitialState);
	Console.WriteLine("Final State:");
	PrintState(test.FinalState);
	return;

	static void PrintState(TestDataState state)
	{
		Console.WriteLine($"  A: 0x{state.RegA:X2}");
		Console.WriteLine($"  X: 0x{state.RegX:X2}");
		Console.WriteLine($"  Y: 0x{state.RegY:X2}");
		Console.WriteLine($"  PC: 0x{state.RegPC:X4}");
		Console.WriteLine($"  SP low: 0x{state.RegSPLow:X2}");
		Console.WriteLine($"  Status: 0b{state.RegStatus:B8}");

		foreach (var ram in state.Ram)
			Console.WriteLine($"    [0x{ram.Address:X4}] = 0x{ram.Value:X2}");
	}
}

static void TestSpinSpeed()
{
	var bus = new TestBus();
	var cpu = new CPU(bus);

	// $8000: EA		(NOP)
	// $8001: 4C 00 80	(JMP $8000)
	bus.Ram[0x8000] = 0xEA;
	bus.Ram[0x8001] = 0x4C;
	bus.Ram[0x8002] = 0x00;
	bus.Ram[0x8003] = 0x80;

	// Set Reset Vector to $8000
	bus.Ram[0xFFFC] = 0x00;
	bus.Ram[0xFFFD] = 0x80;

	cpu.Reset();

	for (var i = 0; i < 7; i++)
		cpu.Tick();

	const long testCycles = 100_000_000L;
	var sw = Stopwatch.StartNew();

	for (long i = 0; i < testCycles; i++)
		cpu.Tick();

	sw.Stop();

	var elapsedMillis = sw.Elapsed.TotalMilliseconds;

	Console.WriteLine($"Time: {elapsedMillis:F2}ms");
	Console.WriteLine($"Cycles: {testCycles}");
	Console.WriteLine($"Speed: {testCycles * 1000.0 / elapsedMillis:N0} cycles/sec");
}
