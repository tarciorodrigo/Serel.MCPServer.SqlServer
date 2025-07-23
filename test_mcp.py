#!/usr/bin/env python3
"""
Script de teste para o MCP Server SQL Server
"""
import json
import subprocess
import sys
import os
import time

def test_mcp_server():
    # Primeiro, tentar build do projeto
    print("Fazendo build do projeto...")
    try:
        build_result = subprocess.run(
            ["dotnet", "build", "--configuration", "Release"],
            capture_output=True,
            text=True,
            cwd=os.getcwd()
        )
        
        if build_result.returncode != 0:
            print(f"Erro no build:")
            print(f"stdout: {build_result.stdout}")
            print(f"stderr: {build_result.stderr}")
            return False
        else:
            print("Build concluído com sucesso!")
    except Exception as e:
        print(f"Erro ao executar build: {e}")
        return False
    
    # Caminho para o executável
    mcp_server_path = ["dotnet", "run", "--configuration", "Release"]
    
    # Mensagens de teste
    test_messages = [
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {
                    "name": "test-client",
                    "version": "1.0.0"
                }
            }
        },
        {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/list"
        }
    ]
    
    print("Iniciando teste do MCP Server...")
    
    try:
        # Iniciar o processo
        process = subprocess.Popen(
            mcp_server_path,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            cwd=os.getcwd()
        )
        
        print("Processo iniciado. Aguardando inicialização...")
        time.sleep(2)  # Dar tempo para inicialização
        
        for i, message in enumerate(test_messages):
            print(f"\n--- Teste {i+1}: {message['method']} ---")
            
            # Enviar mensagem
            message_json = json.dumps(message) + '\n'
            print(f"Enviando: {message_json.strip()}")
            
            process.stdin.write(message_json)
            process.stdin.flush()
            
            # Ler resposta com timeout
            try:
                # Usar poll para verificar se o processo ainda está rodando
                if process.poll() is not None:
                    print("✗ Processo terminou inesperadamente")
                    stderr_output = process.stderr.read()
                    if stderr_output:
                        print(f"Stderr: {stderr_output}")
                    break
                
                response_line = process.stdout.readline()
                if response_line:
                    print(f"Resposta: {response_line.strip()}")
                    
                    # Tentar parsear JSON para validar
                    try:
                        response_data = json.loads(response_line)
                        print("✓ JSON válido")
                        
                        if "error" in response_data:
                            print(f"⚠ Erro retornado: {response_data['error']}")
                        elif "result" in response_data:
                            print("✓ Resultado retornado com sucesso")
                    except json.JSONDecodeError as e:
                        print(f"✗ JSON inválido: {e}")
                        print(f"Conteúdo: {response_line}")
                        
                        # Se não for JSON, pode ser uma mensagem de erro do dotnet
                        if "error" in response_line.lower() or "exception" in response_line.lower():
                            print("✗ Possível erro de execução do .NET")
                else:
                    print("✗ Nenhuma resposta recebida")
                    
            except Exception as e:
                print(f"✗ Erro ao ler resposta: {e}")
        
        # Fechar processo
        try:
            process.stdin.close()
            process.terminate()
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
        
        # Verificar stderr para erros
        stderr_output = process.stderr.read()
        if stderr_output:
            print(f"\nErros no stderr:\n{stderr_output}")
            
    except Exception as e:
        print(f"Erro ao executar teste: {e}")
        return False
    
    print("\nTeste concluído!")
    return True

def check_prerequisites():
    """Verificar se os pré-requisitos estão instalados"""
    print("Verificando pré-requisitos...")
    
    # Verificar dotnet
    try:
        result = subprocess.run(["dotnet", "--version"], capture_output=True, text=True)
        if result.returncode == 0:
            print(f"✓ .NET SDK encontrado: {result.stdout.strip()}")
        else:
            print("✗ .NET SDK não encontrado")
            return False
    except FileNotFoundError:
        print("✗ .NET SDK não encontrado no PATH")
        return False
    
    # Verificar se é .NET 9
    try:
        result = subprocess.run(["dotnet", "--list-sdks"], capture_output=True, text=True)
        if "9." in result.stdout:
            print("✓ .NET 9 SDK encontrado")
        else:
            print("⚠ .NET 9 SDK não encontrado, mas pode funcionar com outras versões")
    except:
        pass
    
    # Verificar se o arquivo do projeto existe
    if os.path.exists("Serel.MCPServer.SqlServer.csproj"):
        print("✓ Arquivo do projeto encontrado")
    else:
        print("✗ Arquivo Serel.MCPServer.SqlServer.csproj não encontrado")
        return False
    
    return True

if __name__ == "__main__":
    if not check_prerequisites():
        print("\nPré-requisitos não atendidos. Verifique a instalação.")
        sys.exit(1)
    
    success = test_mcp_server()
    sys.exit(0 if success else 1)